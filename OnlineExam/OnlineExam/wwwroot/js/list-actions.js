// Generic list actions handling for export, delete, copy, hide, column toggles and shortcuts
(function () {
    function init(rootSelector) {
        const root = document.querySelector(rootSelector) || document;

        const exportBtn = root.querySelector('.list-actions-export-btn');
        const exportMenu = root.querySelector('.list-actions-export-menu');
        const gearBtn = root.querySelector('.list-actions-gear-btn');
        const gearMenu = root.querySelector('.list-actions-gear-menu');
        const deleteBtn = root.querySelector('.list-actions-delete-btn');
        const copyBtn = root.querySelector('.list-actions-copy-btn');
        const hideBtn = root.querySelector('.list-actions-hide-btn');
        const showHiddenBtn = root.querySelector('.list-actions-show-hidden-btn');
        const exportFormatButtons = root.querySelectorAll('.list-actions-export-format');
        const colToggles = root.querySelectorAll('.list-actions-col-toggle');

        const selectAll = document.getElementById('check-all-students') || document.getElementById('selectAll');
        const rowCheckboxSelector = '.student-select-checkbox, .row-checkbox';

        function getSelectedIds() {
            const boxes = Array.from(document.querySelectorAll(rowCheckboxSelector)).filter(cb => cb.checked);
            return boxes.map(cb => cb.value);
        }

        function toggleMenu(menuToOpen, menuToClose) {
            if (!menuToOpen) return;
            menuToOpen.classList.toggle('hidden');
            if (menuToClose) menuToClose.classList.add('hidden');
        }

        if (gearBtn && gearMenu) {
            gearBtn.addEventListener('click', (e) => { e.stopPropagation(); toggleMenu(gearMenu, exportMenu); });
        }
        if (exportBtn && exportMenu) {
            exportBtn.addEventListener('click', (e) => { e.stopPropagation(); toggleMenu(exportMenu, gearMenu); });
        }
        [gearMenu, exportMenu].forEach(m => { if (m) m.addEventListener('click', e => e.stopPropagation()); });
        document.addEventListener('click', () => { if (gearMenu) gearMenu.classList.add('hidden'); if (exportMenu) exportMenu.classList.add('hidden'); });

        // Export CSV
        exportFormatButtons.forEach(btn => {
            btn.addEventListener('click', () => {
                const format = btn.getAttribute('data-format') || 'csv';
                const selected = getSelectedIds();
                if (selected.length === 0) {
                    if (window.appToast) window.appToast.error('Vui lòng chọn ít nhất 1 mục để xuất file.');
                    return;
                }
                const classroomId = document.querySelector('input[name="id"]').value;
                const url = `/Teacher/ExportClassStudents?id=${encodeURIComponent(classroomId)}&studentIds=${selected.join(',')}&format=${format}`;
                window.location.href = url;
            });
        });

        // Delete (AJAX)
        async function execDelete(ids) {
            if (!ids || ids.length === 0) return;
            try {
                const classroomId = document.querySelector('input[name="id"]').value;
                const resp = await fetch('/Teacher/DeleteStudents', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ id: parseInt(classroomId, 10), studentIds: ids })
                });
                if (resp.ok) {
                    window.location.reload();
                } else {
                    if (window.appToast) window.appToast.error('Lỗi khi xóa.');
                }
            } catch (err) {
                console.error(err);
                if (window.appToast) window.appToast.error('Có lỗi xảy ra.');
            }
        }

        if (deleteBtn) {
            deleteBtn.addEventListener('click', () => {
                const ids = getSelectedIds();
                if (ids.length === 0) { if (window.appToast) window.appToast.error('Vui lòng chọn ít nhất 1 mục để xóa.'); return; }
                if (window.appConfirm) window.appConfirm.show('Xác nhận', `Bạn có chắc chắn muốn xóa ${ids.length} mục đã chọn?`, () => execDelete(ids.map(i => parseInt(i, 10))));
            });
        }

        if (copyBtn) {
            copyBtn.addEventListener('click', () => {
                const ids = getSelectedIds();
                if (ids.length === 0) { if (window.appToast) window.appToast.error('Vui lòng chọn ít nhất 1 mục để sao chép.'); return; }
                // copy action may be implemented per-screen; trigger default POST to CopyExams if present
                fetch('/Teacher/CopyExams', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(ids.map(i => parseInt(i,10))) })
                    .then(r => { if (r.ok) window.location.reload(); else if (window.appToast) window.appToast.error('Lỗi khi sao chép.'); })
                    .catch(()=> { if (window.appToast) window.appToast.error('Lỗi khi sao chép.'); });
            });
        }

        // Column toggles
        colToggles.forEach(toggle => {
            const colId = toggle.getAttribute('data-col');
            const colClass = '.col-' + colId;
            const elements = document.querySelectorAll(colClass);
            const saved = localStorage.getItem('hide_col_' + colId);
            if (saved === 'true') { toggle.checked = false; elements.forEach(e => e.style.display = 'none'); }
            toggle.addEventListener('change', function () {
                const checked = this.checked;
                if (!checked) localStorage.setItem('hide_col_' + colId, 'true'); else localStorage.removeItem('hide_col_' + colId);
                elements.forEach(el => el.style.display = checked ? '' : 'none');
            });
        });

        // Hide selected students (client-side persistent via localStorage per classroom)
        if (hideBtn) {
            hideBtn.addEventListener('click', () => {
                const ids = getSelectedIds();
                if (ids.length === 0) { if (window.appToast) window.appToast.error('Vui lòng chọn ít nhất 1 mục để ẩn.'); return; }
                const classroomId = document.querySelector('input[name="id"]').value;
                const key = `hidden_students_${classroomId}`;
                const existing = JSON.parse(localStorage.getItem(key) || '[]');
                const merged = Array.from(new Set(existing.concat(ids)));
                localStorage.setItem(key, JSON.stringify(merged));
                // remove rows from DOM
                merged.forEach(id => {
                    const selector = `tr[data-student-row-id="${id}"]`;
                    const tr = document.querySelector(selector) || document.querySelector(`input[value="${id}"]`)?.closest('tr');
                    if (tr) tr.style.display = 'none';
                });
                if (showHiddenBtn) showHiddenBtn.classList.remove('hidden');
            });
        }

        if (showHiddenBtn) {
            showHiddenBtn.addEventListener('click', () => {
                const classroomId = document.querySelector('input[name="id"]').value;
                const key = `hidden_students_${classroomId}`;
                localStorage.removeItem(key);
                // show all rows
                document.querySelectorAll('tr[data-student-row-id]').forEach(tr => tr.style.display = '');
                showHiddenBtn.classList.add('hidden');
            });
        }

        // On init: hide rows according to localStorage
        document.addEventListener('DOMContentLoaded', () => {
            try {
                const classroomId = document.querySelector('input[name="id"]').value;
                const key = `hidden_students_${classroomId}`;
                const hidden = JSON.parse(localStorage.getItem(key) || '[]');
                if (hidden.length > 0) {
                    hidden.forEach(id => {
                        const tr = document.querySelector(`tr[data-student-row-id="${id}"]`) || document.querySelector(`input[value="${id}"]`)?.closest('tr');
                        if (tr) tr.style.display = 'none';
                    });
                    if (showHiddenBtn) showHiddenBtn.classList.remove('hidden');
                }
            } catch (e) { }
        });

        // Keyboard shortcuts (Alt+X delete, Alt+E export)
        document.addEventListener('keydown', (e) => {
            if (e.altKey && (e.key === 'x' || e.key === 'X')) {
                e.preventDefault();
                const ids = getSelectedIds(); if (ids.length === 0) { if (window.appToast) window.appToast.error('Vui lòng chọn ít nhất 1 mục.'); return; }
                if (window.appConfirm) window.appConfirm.show('Xác nhận', `Bạn có chắc chắn muốn xóa ${ids.length} mục đã chọn?`, () => execDelete(ids.map(i=>parseInt(i,10))));
            }
            if (e.altKey && (e.key === 'e' || e.key === 'E')) {
                e.preventDefault();
                const ids = getSelectedIds(); if (ids.length === 0) { if (window.appToast) window.appToast.error('Vui lòng chọn ít nhất 1 mục.'); return; }
                // default to CSV export
                const classroomId = document.querySelector('input[name="id"]').value;
                window.location.href = `/Teacher/ExportClassStudents?id=${encodeURIComponent(classroomId)}&studentIds=${ids.join(',')}&format=csv`;
            }
        });
    }

    // expose init
    window.ListActions = { init };
})();
