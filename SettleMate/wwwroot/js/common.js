/* Shared SettleMate UI components. */
'use strict';

window.ui = (function () {
    function findElement(s, root = document) {
        return root.querySelector(s);
    }
    function findElements(s, root = document) {
        return Array.from(root.querySelectorAll(s));
    }
    function toast(type, title, message) {
        if (['success', 'error', 'warning', 'info'].includes(type)) {
            type = type;
        } else {
            type = 'info';
        }
        const stack = findElement('#toast-stack');
        if (!stack) {
            return;
        }
        while (stack.children.length >= 3) {
            stack.firstElementChild.remove();
        }
        const card = document.createElement('div');
        card.className = `toast ${type}`;
        card.setAttribute('role', type === 'error' ? 'alert' : 'status');
        const body = document.createElement('div');
        const heading = document.createElement('strong');
        const text = document.createElement('p');
        const close = document.createElement('button');
        heading.textContent = title;
        text.textContent = message;
        close.textContent = '×';
        close.className = 'icon-button';
        close.type = 'button';
        close.setAttribute('aria-label', 'Dismiss notification');
        body.append(heading, text);
        card.append(body, close);
        stack.append(card);
        let remaining = type === 'error' ? 6000 : 4000;
        let started = Date.now();
        let timer;
        function resume() {
            clearTimeout(timer);
            started = Date.now();
            timer = setTimeout(function () {
                return card.remove();
            }, remaining);
        }
        function pause() {
            clearTimeout(timer);
            remaining = Math.max(0, remaining - (Date.now() - started));
        }
        card.addEventListener('mouseenter', pause);
        card.addEventListener('mouseleave', resume);
        card.addEventListener('focusin', pause);
        card.addEventListener('focusout', resume);
        close.onclick = function () {
            clearTimeout(timer);
            card.remove();
        };
        resume();
    }
    function confirm(options = {}) {
        const dialog = findElement('#confirm-dialog');
        if (!dialog || dialog.open) {
            return Promise.resolve({
                confirmed: false,
                reason: '',
            });
        }
        const previous = document.activeElement;
        const reason = findElement('#confirm-reason');
        const accept = findElement('#confirm-accept');
        const cancel = findElement('#confirm-cancel');
        findElement('#confirm-title').textContent = options.title || 'Please confirm';
        findElement('#confirm-message').textContent = options.message || '';
        findElement('#confirm-reason-wrap').hidden = !options.askReason;
        reason.required = !!options.askReason;
        reason.value = '';
        reason.setCustomValidity('');
        accept.textContent = options.confirmText || 'Confirm';
        accept.className = `button ${options.danger ? 'danger' : 'primary'}`;
        cancel.hidden = !!options.success;
        dialog.returnValue = '';
        dialog.showModal();
        (options.success ? accept : cancel).focus();
        return new Promise(function (resolve) {
            return dialog.addEventListener(
                'close',
                function () {
                    if (previous != null) {
                        previous.focus();
                    }
                    resolve({
                        confirmed: dialog.returnValue === 'confirm',
                        reason: reason.value.trim(),
                    });
                },
                {
                    once: true,
                },
            );
        });
    }
    function init() {
        updateIcons();
        findElement('#confirm-cancel')?.addEventListener('click', function () {
            return findElement('#confirm-dialog').close('cancel');
        });
        findElement('#confirm-dialog')?.addEventListener('click', function (e) {
            if (e.target !== e.currentTarget) {
                return;
            }
            const r = e.currentTarget.getBoundingClientRect();
            if (
                e.clientX < r.left ||
                e.clientX > r.right ||
                e.clientY < r.top ||
                e.clientY > r.bottom
            ) {
                e.currentTarget.close('cancel');
            }
        });
        findElement('#confirm-form')?.addEventListener('submit', function (e) {
            const r = findElement('#confirm-reason');
            if (r.required && !r.value.trim()) {
                e.preventDefault();
                r.setCustomValidity('Enter a reason.');
                r.reportValidity();
            }
        });
        findElement('#confirm-reason')?.addEventListener('input', function (e) {
            return e.target.setCustomValidity('');
        });
        const server = findElement('#server-toast');
        if (server) {
            toast(server.dataset.type, server.dataset.title, server.dataset.message);
        }
        findElements('[data-menu]').forEach(function (b) {
            return b.addEventListener('click', function () {
                const open = findElement('#' + b.dataset.menu).classList.toggle('open');
                b.setAttribute('aria-expanded', String(open));
            });
        });
        let drawer;
        let opener;
        function closeDrawer() {
            if (!drawer) {
                return;
            }
            drawer.classList.remove('open');
            drawer.removeAttribute('role');
            drawer.removeAttribute('aria-modal');
            findElement('.drawer-backdrop').hidden = true;
            if (opener != null) {
                opener.setAttribute('aria-expanded', 'false');
            }
            document.body.style.overflow = '';
            if (opener != null) {
                opener.focus();
            }
            drawer = null;
        }
        findElements('[data-drawer]').forEach(function (b) {
            return b.addEventListener('click', function () {
                opener = b;
                drawer = findElement('#' + b.dataset.drawer);
                drawer.classList.add('open');
                drawer.setAttribute('role', 'dialog');
                drawer.setAttribute('aria-modal', 'true');
                findElement('.drawer-backdrop').hidden = false;
                b.setAttribute('aria-expanded', 'true');
                document.body.style.overflow = 'hidden';
                findElement('a', drawer).focus();
            });
        });
        findElements('[data-drawer-close]').forEach(function (b) {
            return b.addEventListener('click', closeDrawer);
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeDrawer();
                findElements('[data-menu]').forEach(function (b) {
                    findElement('#' + b.dataset.menu).classList.remove('open');
                    b.setAttribute('aria-expanded', 'false');
                });
            }
            if (drawer && e.key === 'Tab') {
                const a = findElements('a,button', drawer).filter(function (x) {
                    return x.offsetParent !== null;
                });
                const first = a[0];
                const last = a.at(-1);
                if (e.shiftKey && document.activeElement === first) {
                    e.preventDefault();
                    last.focus();
                } else if (!e.shiftKey && document.activeElement === last) {
                    e.preventDefault();
                    first.focus();
                }
            }
        });
        findElement('.sidebar')?.addEventListener('click', function (e) {
            if (e.target.closest('a')) {
                closeDrawer();
            }
        });
        window.addEventListener('resize', function () {
            if (window.innerWidth >= 1024) {
                closeDrawer();
            }
        });
        findElements('[data-counter]').forEach(function (el) {
            function update() {
                findElement('#' + el.dataset.counter).textContent =
                    `${el.value.length} / ${el.maxLength}`;
            }
            el.addEventListener('input', update);
            update();
        });
        findElements('[data-validate]').forEach(function (form) {
            form.noValidate = true;
            function validate(el) {
                if (!el.willValidate) {
                    return true;
                }
                const valid = el.checkValidity();
                el.setAttribute('aria-invalid', String(!valid));
                let msg = el.parentElement.querySelector('[data-client-error]');
                if (!msg) {
                    msg = document.createElement('span');
                    msg.className = 'field-validation-error';
                    msg.dataset.clientError = '';
                    msg.id = el.id + '-client-error';
                    el.insertAdjacentElement('afterend', msg);
                    el.setAttribute(
                        'aria-describedby',
                        ((el.getAttribute('aria-describedby') || '') + ' ' + msg.id).trim(),
                    );
                }
                if (valid) {
                    msg.textContent = '';
                } else {
                    msg.textContent = el.validationMessage;
                }
                return valid;
            }
            findElements('input,textarea,select', form).forEach(function (el) {
                el.addEventListener('blur', function () {
                    return validate(el);
                });
                el.addEventListener('input', function () {
                    if (el.getAttribute('aria-invalid') === 'true') {
                        validate(el);
                    }
                });
            });
            form.addEventListener('submit', function (e) {
                const invalid = findElements('input,textarea,select', form).filter(function (el) {
                    return !validate(el);
                });
                if (invalid.length) {
                    e.preventDefault();
                    invalid[0].focus();
                }
            });
        });
        findElements('form[data-confirm]').forEach(function (form) {
            return form.addEventListener('submit', async function (e) {
                if (form.dataset.confirmed === 'true') {
                    return;
                }
                e.preventDefault();
                const r = await confirm({
                    title: form.dataset.confirm,
                    message: form.dataset.message,
                    confirmText: form.dataset.confirmText,
                    danger: form.dataset.danger === 'true',
                    askReason: form.dataset.askReason === 'true',
                });
                if (r.confirmed) {
                    let field = findElement('[name=Reason]', form);
                    if (!field) {
                        field = document.createElement('input');
                        field.type = 'hidden';
                        field.name = 'Reason';
                        form.append(field);
                    }
                    field.value = r.reason;
                    form.dataset.confirmed = 'true';
                    form.requestSubmit(e.submitter);
                }
            });
        });
        findElements('[data-upload]').forEach(function (box) {
            const input = findElement('input[type=file]', box);
            const previews = findElement('[data-previews]', box);
            const error = findElement('[data-upload-error]', box);
            const max = Number(box.dataset.maxCount);
            const limit = Number(box.dataset.maxSize);
            let files = [];
            let urls = [];
            function render() {
                urls.forEach(URL.revokeObjectURL);
                urls = [];
                previews.replaceChildren();
                const dt = new DataTransfer();
                files.forEach(function (f, i) {
                    dt.items.add(f);
                    const figure = document.createElement('figure');
                    figure.className = 'file-preview';
                    if (f.type.startsWith('image/')) {
                        const img = document.createElement('img');
                        const url = URL.createObjectURL(f);
                        urls.push(url);
                        img.src = url;
                        img.alt = f.name;
                        figure.append(img);
                    }
                    const name = document.createElement('figcaption');
                    const remove = document.createElement('button');
                    name.textContent = f.name;
                    remove.type = 'button';
                    remove.textContent = 'Remove';
                    remove.setAttribute('aria-label', `Remove ${f.name}`);
                    remove.onclick = function () {
                        files.splice(i, 1);
                        render();
                    };
                    figure.append(name, remove);
                    previews.append(figure);
                });
                input.files = dt.files;
                findElement('[data-upload-count]', box).textContent =
                    `Selected: ${files.length} / ${max}`;
            }
            function add(incoming) {
                error.textContent = '';
                const allowed = input.accept.split(',').map(function (x) {
                    return x.trim().toLowerCase();
                });
                for (const f of incoming) {
                    const extension = '.' + f.name.split('.').pop().toLowerCase();
                    if (files.length >= max) {
                        error.textContent = `Choose up to ${max} files.`;
                        break;
                    }
                    if (f.size > limit || !allowed.includes(extension)) {
                        error.textContent = 'Choose an allowed file type no larger than 5 MB.';
                        continue;
                    }
                    files.push(f);
                }
                render();
            }
            input.addEventListener('change', function () {
                return add([...input.files]);
            });
            findElement('[data-upload-clear]', box).addEventListener('click', function () {
                files = [];
                error.textContent = '';
                render();
            });
            const drop = findElement('.upload-box', box);
            drop.addEventListener('dragover', function (e) {
                return e.preventDefault();
            });
            drop.addEventListener('drop', function (e) {
                e.preventDefault();
                add([...e.dataTransfer.files]);
            });
            window.addEventListener('pagehide', function () {
                return urls.forEach(URL.revokeObjectURL);
            });
        });
        findElements('[data-gallery-src]').forEach(function (b) {
            return b.addEventListener('click', function () {
                const gallery = b.closest('.image-gallery');
                const img = findElement('.gallery-main', gallery);
                img.src = b.dataset.gallerySrc;
                img.alt = b.dataset.galleryAlt;
                findElements('[data-gallery-src]', gallery).forEach(function (x) {
                    return x.setAttribute('aria-pressed', String(x === b));
                });
            });
        });
    }
    document.addEventListener('DOMContentLoaded', init);
    return {
        toast,
        confirm,
    };
})();
('use strict');