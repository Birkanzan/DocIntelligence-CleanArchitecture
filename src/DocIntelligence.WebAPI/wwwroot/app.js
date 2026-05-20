/* ═══════════════════════════════════════════════════════════
   DocIntelligence — app.js
   API Base URL: http://localhost:5000  (appsettings.json'dan)
   ═══════════════════════════════════════════════════════════ */

const API = '';

/* ─── State ──────────────────────────────────────────────── */
let currentPage = 1;
let totalPages  = 1;
let autoRefreshTimer = null;

/* ─── Init ───────────────────────────────────────────────── */
document.addEventListener('DOMContentLoaded', () => {
  initNav();
  initUpload();
  initSearch();
  checkApiStatus();
  loadDashboard();
  setInterval(checkApiStatus, 15_000);
});

/* ──────────────────────────────────────────────────────────
   NAVIGATION
   ────────────────────────────────────────────────────────── */
function initNav() {
  document.querySelectorAll('.nav-item').forEach(btn => {
    btn.addEventListener('click', () => showPage(btn.dataset.page));
  });
}

function showPage(name) {
  document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
  document.querySelectorAll('.nav-item').forEach(b => b.classList.remove('active'));

  document.getElementById(`page-${name}`).classList.add('active');
  document.getElementById(`nav-${name}`).classList.add('active');

  const titles = { dashboard: 'Dashboard', upload: 'Belge Yükle', documents: 'Belgelerim' };
  document.getElementById('page-title').textContent = titles[name] ?? name;

  if (name === 'dashboard') loadDashboard();
  if (name === 'documents') loadDocuments();
}

/* ──────────────────────────────────────────────────────────
   API STATUS CHECK
   ────────────────────────────────────────────────────────── */
async function checkApiStatus() {
  const dot  = document.getElementById('status-dot');
  const text = document.getElementById('status-text');
  try {
    const r = await fetch(`${API}/api/documents?pageSize=1`, { signal: AbortSignal.timeout(4000) });
    if (r.ok || r.status === 400) {
      dot.className  = 'status-dot online';
      text.textContent = 'API çevrimiçi';
    } else throw new Error('non-ok');
  } catch {
    dot.className  = 'status-dot offline';
    text.textContent = 'API çevrimdışı';
  }
}

/* ──────────────────────────────────────────────────────────
   DASHBOARD
   ────────────────────────────────────────────────────────── */
async function loadDashboard() {
  try {
    const data = await apiFetch('/api/documents?pageSize=1000');
    if (!data) return;

    const docs = data.items ?? [];
    const total = data.totalCount ?? docs.length;

    const completed  = docs.filter(d => d.status === 'Completed').length;
    const processing = docs.filter(d =>
      ['Pending','Optimizing','ExtractingText','Classifying'].includes(d.status)).length;

    document.getElementById('total-docs').textContent = total;
    document.getElementById('completed-docs').textContent = completed;
    document.getElementById('processing-docs').textContent = processing;

    // Compression stats
    const withOpt = docs.filter(d => d.optimizedSizeBytes && d.fileSizeBytes);
    let origTotal = 0, optTotal = 0;
    withOpt.forEach(d => { origTotal += d.fileSizeBytes; optTotal += d.optimizedSizeBytes; });

    const savedBytes = origTotal - optTotal;
    document.getElementById('saved-bytes').textContent = formatBytes(Math.max(0, savedBytes));
    document.getElementById('orig-total').textContent = formatBytes(origTotal);
    document.getElementById('opt-total').textContent  = formatBytes(optTotal);

    const avgSaving = origTotal > 0 ? ((origTotal - optTotal) / origTotal * 100) : 0;
    document.getElementById('avg-saving').textContent = `${Math.max(0, avgSaving).toFixed(0)}%`;
    setDonut(Math.max(0, Math.min(100, avgSaving)));

    // Recent table (last 8)
    const recent = [...docs].sort((a,b) => new Date(b.uploadedAt) - new Date(a.uploadedAt)).slice(0,8);
    renderRecentTable(recent);

  } catch(e) {
    console.error(e);
  }
}

function setDonut(pct) {
  const circ = 2 * Math.PI * 46; // r=46
  const fill = document.getElementById('donut-fill');
  fill.setAttribute('stroke-dasharray', `${(pct/100)*circ} ${circ}`);
}

function renderRecentTable(docs) {
  const tbody = document.getElementById('recent-tbody');
  if (!docs.length) {
    tbody.innerHTML = '<tr><td colspan="5" class="empty-cell">Henüz belge yok</td></tr>';
    return;
  }
  tbody.innerHTML = docs.map(d => `
    <tr>
      <td><span style="font-weight:500">${escHtml(d.originalFileName)}</span></td>
      <td>${statusBadge(d.status)}</td>
      <td>${d.category ? `<span class="badge" style="background:rgba(167,139,250,.12);color:var(--purple-lt)">${d.category}</span>` : '<span style="color:var(--text-muted)">—</span>'}</td>
      <td>${formatBytes(d.fileSizeBytes)}</td>
      <td style="color:var(--text-muted)">${formatDate(d.uploadedAt)}</td>
    </tr>`).join('');
}

/* ──────────────────────────────────────────────────────────
   DOCUMENT LIST
   ────────────────────────────────────────────────────────── */
async function loadDocuments(page = 1) {
  currentPage = page;
  const status   = document.getElementById('filter-status').value;
  const category = document.getElementById('filter-category').value;
  const search   = document.getElementById('global-search').value;

  const params = new URLSearchParams({ page, pageSize: 15 });
  if (status)   params.append('status', status);
  if (category) params.append('category', category);
  if (search)   params.append('search', search);

  document.getElementById('docs-tbody').innerHTML =
    '<tr><td colspan="9" class="empty-cell"><div class="spinner-sm"></div></td></tr>';

  const data = await apiFetch(`/api/documents?${params}`);
  if (!data) return;

  const docs  = data.items ?? [];
  totalPages  = Math.ceil((data.totalCount ?? docs.length) / 15) || 1;

  renderDocsTable(docs);
  renderPagination();

  // Auto-refresh if any docs are still processing
  const hasProcessing = docs.some(d =>
    ['Pending','Optimizing','ExtractingText','Classifying'].includes(d.status));
  clearTimeout(autoRefreshTimer);
  if (hasProcessing) autoRefreshTimer = setTimeout(() => loadDocuments(currentPage), 4000);
}

function renderDocsTable(docs) {
  const tbody = document.getElementById('docs-tbody');
  if (!docs.length) {
    tbody.innerHTML = '<tr><td colspan="9" class="empty-cell">Belge bulunamadı</td></tr>';
    return;
  }
  tbody.innerHTML = docs.map(d => {
    const saving = d.fileSizeBytes && d.optimizedSizeBytes
      ? ((d.fileSizeBytes - d.optimizedSizeBytes) / d.fileSizeBytes * 100)
      : null;
    return `
    <tr>
      <td>
        <span class="file-icon">${fileIcon(d.fileType)}</span>
        <span style="font-weight:500;margin-left:6px">${escHtml(d.originalFileName)}</span>
      </td>
      <td>${statusBadge(d.status)}</td>
      <td>${d.category
        ? `<span class="badge" style="background:rgba(167,139,250,.12);color:var(--purple-lt)">${d.category}</span>`
        : '<span style="color:var(--text-muted)">—</span>'}</td>
      <td>${formatBytes(d.fileSizeBytes)}</td>
      <td>${d.optimizedSizeBytes ? formatBytes(d.optimizedSizeBytes) : '<span style="color:var(--text-muted)">—</span>'}</td>
      <td>${saving !== null
        ? `<span class="saving-badge${saving < 0 ? ' negative' : ''}">${saving >= 0 ? '+' : ''}${saving.toFixed(0)}%</span>`
        : '<span style="color:var(--text-muted)">—</span>'}</td>
      <td>${d.classificationConfidence != null
        ? `<span style="color:${d.classificationConfidence > .7 ? 'var(--green-lt)' : 'var(--orange-lt)'}">${(d.classificationConfidence*100).toFixed(0)}%</span>`
        : '<span style="color:var(--text-muted)">—</span>'}</td>
      <td style="color:var(--text-muted)">${formatDate(d.uploadedAt)}</td>
      <td>
        <div style="display:flex;gap:4px">
          <button class="icon-btn" title="Detay" onclick="openDetail('${d.id}')">
            <svg viewBox="0 0 20 20" fill="currentColor"><path d="M10 12a2 2 0 100-4 2 2 0 000 4z"/><path fill-rule="evenodd" d="M.458 10C1.732 5.943 5.522 3 10 3s8.268 2.943 9.542 7c-1.274 4.057-5.064 7-9.542 7S1.732 14.057.458 10zM14 10a4 4 0 11-8 0 4 4 0 018 0z" clip-rule="evenodd"/></svg>
          </button>
          ${d.optimizedStoragePath ? `
          <button class="icon-btn" title="Optimize İndir" onclick="downloadDoc('${d.id}', true)">
            <svg viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M3 17a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zm3.293-7.707a1 1 0 011.414 0L9 10.586V3a1 1 0 112 0v7.586l1.293-1.293a1 1 0 111.414 1.414l-3 3a1 1 0 01-1.414 0l-3-3a1 1 0 010-1.414z" clip-rule="evenodd"/></svg>
          </button>` : ''}
          <button class="icon-btn danger" title="Sil" onclick="deleteDoc('${d.id}','${escHtml(d.originalFileName).replace(/'/g,"\\'")}')">
            <svg viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M9 2a1 1 0 00-.894.553L7.382 4H4a1 1 0 000 2v10a2 2 0 002 2h8a2 2 0 002-2V6a1 1 0 100-2h-3.382l-.724-1.447A1 1 0 0011 2H9zM7 8a1 1 0 012 0v6a1 1 0 11-2 0V8zm5-1a1 1 0 00-1 1v6a1 1 0 102 0V8a1 1 0 00-1-1z" clip-rule="evenodd"/></svg>
          </button>
        </div>
      </td>
    </tr>`;
  }).join('');
}

function renderPagination() {
  const el = document.getElementById('pagination');
  if (totalPages <= 1) { el.innerHTML = ''; return; }

  let html = '';
  const start = Math.max(1, currentPage - 2);
  const end   = Math.min(totalPages, currentPage + 2);

  if (start > 1) html += pageBtn(1) + (start > 2 ? '<span style="color:var(--text-muted);padding:0 4px">…</span>' : '');
  for (let i = start; i <= end; i++) html += pageBtn(i);
  if (end < totalPages) html += (end < totalPages-1 ? '<span style="color:var(--text-muted);padding:0 4px">…</span>' : '') + pageBtn(totalPages);

  el.innerHTML = html;
}

function pageBtn(n) {
  return `<button class="page-btn${n===currentPage?' active':''}" onclick="loadDocuments(${n})">${n}</button>`;
}

/* ──────────────────────────────────────────────────────────
   DETAIL MODAL
   ────────────────────────────────────────────────────────── */
async function openDetail(id) {
  document.getElementById('modal-backdrop').style.display = 'flex';
  document.getElementById('modal-content').innerHTML = '<div style="text-align:center;padding:40px"><div class="spinner-sm" style="margin:auto"></div></div>';

  const d = await apiFetch(`/api/documents/${id}`);
  if (!d) return;

  const saving = d.fileSizeBytes && d.optimizedSizeBytes
    ? ((d.fileSizeBytes - d.optimizedSizeBytes) / d.fileSizeBytes * 100).toFixed(1)
    : null;

  document.getElementById('modal-content').innerHTML = `
    <h2>${escHtml(d.originalFileName)}</h2>
    <div class="detail-grid">
      <div class="detail-field"><label>Durum</label><div class="detail-val">${statusBadge(d.status)}</div></div>
      <div class="detail-field"><label>Kategori</label><div class="detail-val">${d.category ?? '—'}</div></div>
      <div class="detail-field"><label>Dosya Türü</label><div class="detail-val">${d.fileType ?? '—'}</div></div>
      <div class="detail-field"><label>Yükleme Tarihi</label><div class="detail-val">${formatDateLong(d.uploadedAt)}</div></div>
      <div class="detail-field"><label>Orijinal Boyut</label><div class="detail-val">${formatBytes(d.fileSizeBytes)}</div></div>
      <div class="detail-field"><label>Optimize Boyut</label><div class="detail-val">${d.optimizedSizeBytes ? formatBytes(d.optimizedSizeBytes) : '—'}</div></div>
      <div class="detail-field"><label>Boyut Tasarrufu</label><div class="detail-val">${saving !== null ? `<span class="saving-badge">${saving}%</span>` : '—'}</div></div>
      <div class="detail-field"><label>OCR Güveni</label><div class="detail-val">${d.ocrConfidence != null ? `${(d.ocrConfidence*100).toFixed(0)}%` : '—'}</div></div>
      <div class="detail-field"><label>ML Güveni</label><div class="detail-val">${d.classificationConfidence != null ? `${(d.classificationConfidence*100).toFixed(0)}%` : '—'}</div></div>
      <div class="detail-field"><label>Belge ID</label><div class="detail-val" style="font-size:11px;color:var(--text-muted)">${d.id}</div></div>
    </div>
    ${d.extractedText ? `
      <div style="margin-top:16px">
        <label style="font-size:11px;color:var(--text-muted);text-transform:uppercase;letter-spacing:.05em">Çıkarılan Metin</label>
        <div class="detail-text-box">${escHtml(d.extractedText)}</div>
      </div>` : ''}
    ${d.errorMessage ? `
      <div style="margin-top:16px;padding:12px;background:rgba(239,68,68,.08);border:1px solid rgba(239,68,68,.2);border-radius:8px;font-size:12px;color:var(--red-lt)">
        <strong>Hata:</strong> ${escHtml(d.errorMessage)}
      </div>` : ''}
    <div style="margin-top:24px; display:flex; gap:10px;">
        <button class="btn-primary" onclick="downloadDoc('${d.id}', false)">Orijinali İndir</button>
        ${d.optimizedStoragePath ? `<button class="btn-secondary" onclick="downloadDoc('${d.id}', true)">Sıkıştırılmışı İndir</button>` : ''}
    </div>
  `;
}

function downloadDoc(id, optimized) {
    window.open(`${API}/api/documents/${id}/download?optimized=${optimized}`, '_blank');
}

function closeModal() {
  document.getElementById('modal-backdrop').style.display = 'none';
}

/* ──────────────────────────────────────────────────────────
   DELETE
   ────────────────────────────────────────────────────────── */
async function deleteDoc(id, name) {
  if (!confirm(`"${name}" silinsin mi?`)) return;
  try {
    const r = await fetch(`${API}/api/documents/${id}`, { method: 'DELETE' });
    if (r.status === 204) {
      showToast('success', `"${name}" silindi.`);
      loadDocuments(currentPage);
      loadDashboard();
    } else {
      showToast('error', 'Silme işlemi başarısız.');
    }
  } catch {
    showToast('error', 'Sunucuya ulaşılamadı.');
  }
}

/* ──────────────────────────────────────────────────────────
   UPLOAD
   ────────────────────────────────────────────────────────── */
function initUpload() {
  const zone  = document.getElementById('drop-zone');
  const input = document.getElementById('file-input');

  zone.addEventListener('dragover', e => { e.preventDefault(); zone.classList.add('dragging'); });
  zone.addEventListener('dragleave', () => zone.classList.remove('dragging'));
  zone.addEventListener('drop', e => {
    e.preventDefault();
    zone.classList.remove('dragging');
    uploadFiles([...e.dataTransfer.files]);
  });
  zone.addEventListener('click', e => {
    if (e.target.tagName !== 'LABEL' && e.target.tagName !== 'INPUT') input.click();
  });
  input.addEventListener('change', () => uploadFiles([...input.files]));
}

async function uploadFiles(files) {
  if (!files.length) return;

  const queue = document.getElementById('upload-queue');
  const items = document.getElementById('queue-items');
  queue.style.display = 'block';

  for (const file of files) {
    const itemId = `qi-${Math.random().toString(36).slice(2)}`;
    items.insertAdjacentHTML('beforeend', `
      <div class="queue-item" id="${itemId}">
        <div class="queue-file-icon">${fileIcon(file.name.split('.').pop().toUpperCase())}</div>
        <div class="queue-info">
          <div class="queue-name">${escHtml(file.name)}</div>
          <div class="queue-size">${formatBytes(file.size)}</div>
          <div class="progress-bar"><div class="progress-fill" id="${itemId}-bar" style="width:0%"></div></div>
        </div>
        <div class="queue-status uploading" id="${itemId}-status">Yükleniyor…</div>
      </div>`);

    await uploadSingleFile(file, itemId);
  }

  loadDashboard();
}

function uploadSingleFile(file, itemId) {
  return new Promise(resolve => {
    const bar    = document.getElementById(`${itemId}-bar`);
    const status = document.getElementById(`${itemId}-status`);

    const fd = new FormData();
    fd.append('file', file);

    const xhr = new XMLHttpRequest();
    xhr.open('POST', `${API}/api/documents/upload`);

    xhr.upload.onprogress = e => {
      if (e.lengthComputable) bar.style.width = `${(e.loaded/e.total*100).toFixed(0)}%`;
    };

    xhr.onload = () => {
      if (xhr.status === 202 || xhr.status === 200) {
        bar.style.width = '100%';
        status.className = 'queue-status success';
        status.textContent = '✓ Yüklendi';
        showToast('success', `"${file.name}" yüklendi, işleniyor.`);
      } else {
        status.className = 'queue-status error';
        status.textContent = `Hata ${xhr.status}`;
        showToast('error', `Yükleme başarısız: ${file.name}`);
      }
      resolve();
    };
    xhr.onerror = () => {
      status.className = 'queue-status error';
      status.textContent = 'Bağlantı hatası';
      showToast('error', 'Sunucuya ulaşılamadı.');
      resolve();
    };

    xhr.send(fd);
  });
}

/* ──────────────────────────────────────────────────────────
   SEARCH
   ────────────────────────────────────────────────────────── */
function initSearch() {
  let debounce;
  document.getElementById('global-search').addEventListener('input', () => {
    clearTimeout(debounce);
    debounce = setTimeout(() => {
      const page = document.getElementById('page-documents');
      if (page.classList.contains('active')) loadDocuments(1);
      else showPage('documents');
    }, 350);
  });

  document.getElementById('filter-status').addEventListener('change', () => loadDocuments(1));
  document.getElementById('filter-category').addEventListener('change', () => loadDocuments(1));
}

/* ──────────────────────────────────────────────────────────
   HELPERS
   ────────────────────────────────────────────────────────── */
async function apiFetch(path) {
  try {
    const r = await fetch(`${API}${path}`, { signal: AbortSignal.timeout(8000) });
    if (!r.ok) { console.warn('API error', r.status); return null; }
    return await r.json();
  } catch(e) {
    console.warn('fetch failed', e);
    return null;
  }
}

function escHtml(s = '') {
  return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function formatBytes(b) {
  if (b == null || b === 0) return '—';
  if (b < 1024) return `${b} B`;
  if (b < 1048576) return `${(b/1024).toFixed(1)} KB`;
  return `${(b/1048576).toFixed(2)} MB`;
}

function formatDate(s) {
  if (!s) return '—';
  const d = new Date(s);
  return d.toLocaleDateString('tr-TR', { day:'2-digit', month:'2-digit', year:'numeric' });
}

function formatDateLong(s) {
  if (!s) return '—';
  return new Date(s).toLocaleString('tr-TR');
}

function statusBadge(status) {
  const map = {
    Pending:       ['pending', 'Bekliyor'],
    Optimizing:    ['processing pulse', 'Optimize'],
    ExtractingText:['processing pulse', 'OCR'],
    Classifying:   ['processing pulse', 'ML'],
    Completed:     ['completed', 'Tamamlandı'],
    Failed:        ['failed', 'Hata'],
  };
  const [cls, label] = map[status] ?? ['pending', status];
  return `<span class="badge ${cls}">${label}</span>`;
}

function fileIcon(type = '') {
  const t = type.toUpperCase();
  if (t.includes('PDF'))  return '📄';
  if (t.includes('PNG'))  return '🖼️';
  if (t.includes('JPEG') || t.includes('JPG')) return '📷';
  if (t.includes('TIFF')) return '🖼️';
  if (t.includes('BMP'))  return '🖼️';
  return '📁';
}

function showToast(type, msg) {
  const ct = document.getElementById('toast-container');
  const el = document.createElement('div');
  el.className = `toast ${type}`;
  el.innerHTML = `<span class="toast-icon">${type === 'success' ? '✅' : '❌'}</span><span>${escHtml(msg)}</span>`;
  ct.appendChild(el);
  setTimeout(() => el.remove(), 4000);
}
