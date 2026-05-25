document.addEventListener('DOMContentLoaded', function () {
    try {
        const inputs = Array.from(document.querySelectorAll('input[type="password"]'));
        inputs.forEach(input => {
            // Skip inputs that explicitly opt-out
            if (input.classList.contains('no-password-toggle')) return;

            // If already wrapped, skip
            if (input.closest('.pw-toggle-wrap')) return;

            const wrapper = document.createElement('div');
            wrapper.className = 'pw-toggle-wrap relative';
            input.parentNode.insertBefore(wrapper, input);
            wrapper.appendChild(input);

            // Ensure input has padding on the right so button doesn't overlap
            const currentPaddingRight = window.getComputedStyle(input).paddingRight || '0px';
            if (!input.classList.contains('pw-toggle-adjusted')) {
                input.style.paddingRight = '3rem';
                input.classList.add('pw-toggle-adjusted');
            }

            const btn = document.createElement('button');
            btn.type = 'button';
            btn.setAttribute('aria-label', 'Hiện/ẩn mật khẩu');
            btn.className = 'pw-toggle-btn absolute right-2 top-1/2 -translate-y-1/2 bg-transparent border-none text-slate-400 hover:text-slate-600';
            btn.innerHTML = '<span class="material-symbols-outlined">visibility</span>';

            btn.addEventListener('click', function (e) {
                e.preventDefault();
                if (input.type === 'password') {
                    input.type = 'text';
                    btn.innerHTML = '<span class="material-symbols-outlined">visibility_off</span>';
                } else {
                    input.type = 'password';
                    btn.innerHTML = '<span class="material-symbols-outlined">visibility</span>';
                }
                input.focus();
            });

            wrapper.appendChild(btn);
        });
    } catch (e) {
        // Fail silently
        console.error('password-toggle error', e);
    }
});
