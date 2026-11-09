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
document.addEventListener('DOMContentLoaded', function () {
    function findElement(s, r = document) {
        return r.querySelector(s);
    }
    function findElements(s, r = document) {
        return [...r.querySelectorAll(s)];
    } // Standard MVC forms retain server validation; these checks improve immediate feedback.
    function validate(field) {
        field.setCustomValidity('');
        const value = field.value;
        if (field.dataset.match && value !== findElement('#' + field.dataset.match)?.value) {
            field.setCustomValidity('Passwords do not match.');
        }
        if (field.dataset.valEqualto) {
            const other = field.form?.elements[field.dataset.valEqualtoOther.replace('*.', '')];
            if (other && value !== other.value) {
                field.setCustomValidity(field.dataset.valEqualto);
            }
        }
        if (
            field.dataset.valRequired &&
            !value.trim() &&
            !['checkbox', 'hidden', 'file'].includes(field.type)
        ) {
            field.setCustomValidity(field.dataset.valRequired);
        }
        if (value && field.dataset.valRegex) {
            try {
                if (!new RegExp(field.dataset.valRegexPattern).test(value)) {
                    field.setCustomValidity(field.dataset.valRegex);
                }
            } catch {}
        }
        if (
            value &&
            field.dataset.valLengthMax &&
            value.length > Number(field.dataset.valLengthMax)
        ) {
            field.setCustomValidity(field.dataset.valLength);
        }
        if (
            value &&
            field.dataset.valLengthMin &&
            value.length < Number(field.dataset.valLengthMin)
        ) {
            field.setCustomValidity(field.dataset.valLength);
        }
        if (
            value &&
            field.dataset.valRange &&
            (Number(value) < Number(field.dataset.valRangeMin) ||
                Number(value) > Number(field.dataset.valRangeMax))
        ) {
            field.setCustomValidity(field.dataset.valRange);
        }
        if (
            field.dataset.maxFile &&
            field.files &&
            [...field.files].some(function (f) {
                return f.size > Number(field.dataset.maxFile);
            })
        ) {
            field.setCustomValidity('Choose a file up to 5 MB.');
        }
    }
    findElements('[data-validate]').forEach(function (form) {
        findElements('input,select,textarea', form).forEach(function (el) {
            el.addEventListener(
                'blur',
                function () {
                    return validate(el);
                },
                true,
            );
            el.addEventListener(
                'input',
                function () {
                    return validate(el);
                },
                true,
            );
        });
        form.addEventListener(
            'submit',
            function (e) {
                if (e.submitter?.value === 'Draft') {
                    e.stopImmediatePropagation();
                    return;
                }
                findElements('input,textarea,select', form).forEach(validate);
                const bad = findElements('input,textarea,select', form).find(function (x) {
                    return x.willValidate && !x.checkValidity();
                });
                if (bad) {
                    e.preventDefault();
                    e.stopImmediatePropagation();
                    bad.closest('[data-step]')?.removeAttribute('hidden');
                    bad.focus();
                    bad.reportValidity();
                    return;
                }
                const otp = findElement('[data-proof]', form);
                if (otp && !otp.value) {
                    e.preventDefault();
                    e.stopImmediatePropagation();
                    ui.toast(
                        'error',
                        'Verify your email',
                        'Enter and verify the email code before continuing.',
                    );
                }
            },
            true,
        );
    });
    findElements('[data-password]').forEach(function (b) {
        return b.addEventListener('click', function () {
            const field = findElement('#' + b.dataset.password);
            if (field.type === 'password') {
                field.type = 'text';
            } else {
                field.type = 'password';
            }
            b.setAttribute(
                'aria-label',
                field.type === 'password' ? 'Show password' : 'Hide password',
            );
        });
    });
    findElements('[data-query-form]').forEach(function (form) {
        const q = new URLSearchParams(location.search);
        findElements('select[name]', form).forEach(function (el) {
            if (q.has(el.name)) {
                el.value = q.get(el.name);
            }
        });
    });
    // OTPs travel only to same-origin endpoints. No codes are stored in browser storage.
    async function post(url, data, form) {
        const token =
            findElement('[name=__RequestVerificationToken]', form)?.value ||
            findElement('[name=__RequestVerificationToken]')?.value;
        const body = new URLSearchParams({
            ...data,
            __RequestVerificationToken: token || '',
        });
        const res = await fetch(url, {
            method: 'POST',
            headers: {
                Accept: 'application/json',
                'Content-Type': 'application/x-www-form-urlencoded',
            },
            body,
        });
        let result;
        try {
            result = await res.json();
        } catch {
            throw new Error(
                res.status === 429
                    ? 'Too many requests. Wait one minute.'
                    : 'The server could not complete the request.',
            );
        }
        if (!res.ok || !result.success) {
            throw new Error(result.message || 'Please try again.');
        }
        return result;
    }
    findElements('.otp-live').forEach(function (box) {
        const form = box.closest('form');
        const email = findElement('[data-email]', form);
        const send = findElement('[data-send-otp]', box);
        const verify = findElement('[data-verify-otp]', box);
        const digits = findElements('.otp-digits input', box);
        const proof = findElement('[data-proof]', box);
        const error = findElement('[data-otp-error]', box);
        const note = findElement('[data-otp-note]', box);
        let timer;
        email.addEventListener('input', function () {
            proof.value = '';
            note.textContent = '';
            error.textContent = '';
            digits.forEach(function (x) {
                return (x.value = '');
            });
        });
        digits.forEach(function (el, i) {
            el.addEventListener('input', function () {
                el.value = el.value.replace(/\D/g, '').slice(-1);
                if (el.value) {
                    digits[i + 1]?.focus();
                }
            });
            el.addEventListener('keydown', function (e) {
                if (e.key === 'Backspace' && !el.value) {
                    digits[i - 1]?.focus();
                }
            });
            el.addEventListener('paste', function (e) {
                e.preventDefault();
                const v = e.clipboardData.getData('text').replace(/\D/g, '').slice(0, 6);
                digits.forEach(function (d, k) {
                    return (d.value = v[k] || '');
                });
                digits[Math.min(5, v.length)].focus();
            });
        });
        send.addEventListener('click', async function () {
            error.textContent = '';
            proof.value = '';
            if (!email.reportValidity()) {
                return;
            }
            send.disabled = true;
            try {
                const result = await post(
                    '/Account/SendOtp',
                    {
                        email: email.value,
                        purpose: box.dataset.purpose,
                    },
                    form,
                );
                note.textContent = result.message;
                findElement('.otp-entry', box).hidden = false;
                let seconds = 30;
                clearInterval(timer);
                send.textContent = `Resend in ${seconds}s`;
                timer = setInterval(function () {
                    seconds--;
                    if (seconds) {
                        send.textContent = `Resend in ${seconds}s`;
                    } else {
                        send.textContent = 'Resend code';
                    }
                    if (!seconds) {
                        clearInterval(timer);
                        send.disabled = false;
                    }
                }, 1000);
                digits[0].focus();
            } catch (e) {
                error.textContent = e.message;
                send.disabled = false;
            }
        });
        verify.addEventListener('click', async function () {
            error.textContent = '';
            const code = digits
                .map(function (x) {
                    return x.value;
                })
                .join('');
            if (code.length !== 6) {
                error.textContent = 'Enter all six digits.';
                return;
            }
            verify.disabled = true;
            try {
                const result = await post(
                    '/Account/VerifyOtp',
                    {
                        email: email.value,
                        purpose: box.dataset.purpose,
                        code,
                    },
                    form,
                );
                proof.value = result.proof;
                note.textContent = 'Email verified';
                findElement('.otp-entry', box).hidden = true;
            } catch (e) {
                error.textContent = e.message;
            } finally {
                verify.disabled = false;
            }
        });
    });
    findElements('[data-ajax]').forEach(function (form) {
        return form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const button = findElement('button[type=submit],button:not([type])', form);
            if (button) {
                button.disabled = true;
            }
            try {
                const res = await fetch(form.action, {
                    method: 'POST',
                    headers: {
                        Accept: 'application/json',
                    },
                    body: new FormData(form),
                });
                let data;
                try {
                    data = await res.json();
                } catch {
                    throw new Error('Request failed. Please refresh and try again.');
                }
                if (!res.ok || !data.success) {
                    throw new Error(data.message);
                }
                ui.toast('success', 'Updated', data.message);
                if (form.hasAttribute('data-reload')) {
                    location.reload();
                } else if (form.action.includes('/Wishlist/Toggle')) {
                    button?.classList.toggle('saved');
                    if (button != null) {
                        button.setAttribute(
                            'aria-pressed',
                            String(button.classList.contains('saved')),
                        );
                    }
                }
            } catch (err) {
                ui.toast('error', 'Could not update', err.message);
            } finally {
                if (button) {
                    button.disabled = false;
                }
            }
        });
    });
    // Registration keeps all server-bound fields in a single form and validates each step.
    findElements('[data-register]').forEach(function (form) {
        const sections = findElements('[data-step]', form);
        const prev = findElement('[data-prev]', form);
        const next = findElement('[data-next]', form);
        const finish = findElement('[data-finish]', form);
        const pills = findElements('.step-pills span', form);
        let step = 0;
        function show() {
            sections.forEach(function (s, i) {
                return (s.hidden = i !== step);
            });
            pills.forEach(function (p, i) {
                return p.classList.toggle('active', i === step);
            });
            prev.hidden = step === 0;
            next.hidden = step === sections.length - 1;
            finish.hidden = step !== sections.length - 1;
        }
        prev.onclick = function () {
            step = Math.max(0, step - 1);
            show();
        };
        next.onclick = function () {
            const controls = findElements('input,select,textarea', sections[step]);
            controls.forEach(validate);
            const bad = controls.find(function (el) {
                return el.willValidate && !el.checkValidity();
            });
            if (bad) {
                bad.reportValidity();
                bad.focus();
                return;
            }
            if (step === 0 && !findElement('[data-proof]', form).value) {
                ui.toast(
                    'error',
                    'Verify your email',
                    'Complete email verification before continuing.',
                );
                return;
            }
            step++;
            show();
        };
        show();
    });
    findElements('[data-listing-editor]').forEach(function (form) {
        return form.addEventListener('submit', function (e) {
            if (e.submitter?.value === 'Draft') {
                return;
            }
            const kind = findElement('[name=Kind]', form).value;
            if (kind === 'Property') {
                const declarations = [
                    'Declaration1',
                    'Declaration2',
                    'Declaration3',
                    'Declaration4',
                ];
                if (
                    declarations.some(function (n) {
                        return !findElement(`input[type=checkbox][name=${n}]`, form)?.checked;
                    }) ||
                    !findElement('input[name=Amenities]:checked', form)
                ) {
                    e.preventDefault();
                    ui.toast(
                        'error',
                        'Complete verification',
                        'Select an amenity and accept all four declarations.',
                    );
                }
            }
        });
    });
    findElements('[data-listing-editor]').forEach(function (form) {
        const type = findElement('[name=Type]', form);
        const kind = findElement('[name=Kind]', form).value;
        function toggle(name, visible) {
            const input = findElement(`[name=${name}]`, form);
            if (input) {
                input.closest('.field').hidden = !visible;
                input.disabled = !visible;
            }
        }
        function refresh() {
            if (kind === 'Property') {
                toggle('Bhk', type.value === 'Flat');
                toggle('Bedrooms', type.value === 'Flat');
                toggle('Beds', type.value === 'Hostel');
            }
            if (kind === 'Item') {
                toggle('Deposit', type.value === 'Rent');
            }
        }
        type.addEventListener('change', refresh);
        refresh();
    });
    // Native chart drawing adds a visual summary while retaining the accessible data rows.
    findElements('[data-chart]').forEach(function (el) {
        const rows = findElements('.chart-row', el);
        const values = rows.map(function (r) {
            return Number(findElement('strong', r).textContent);
        });
        const max = Math.max(1, ...values);
        if (!values.length) {
            return;
        }
        const ns = 'http://www.w3.org/2000/svg';
        const svg = document.createElementNS(ns, 'svg');
        svg.setAttribute('viewBox', '0 0 400 130');
        svg.setAttribute('aria-hidden', 'true');
        svg.classList.add('summary-chart');
        if (/views|Registrations/.test(el.dataset.chart)) {
            const path = document.createElementNS(ns, 'polyline');
            path.setAttribute(
                'points',
                values
                    .map(function (v, i) {
                        return `${20 + i * (360 / Math.max(1, values.length - 1))},${120 - (v / max) * 100}`;
                    })
                    .join(' '),
            );
            path.setAttribute('fill', 'none');
            path.setAttribute('stroke', 'currentColor');
            path.setAttribute('stroke-width', '3');
            svg.append(path);
        } else {
            // Bars are at most 48 units wide and centred, so one value is not one giant block.
            const slot = 360 / values.length;
            const barWidth = Math.min(48, slot * 0.6);
            // Light grid line along the bottom.
            const base = document.createElementNS(ns, 'line');
            base.setAttribute('x1', '20');
            base.setAttribute('x2', '380');
            base.setAttribute('y1', '120');
            base.setAttribute('y2', '120');
            base.setAttribute('stroke', 'currentColor');
            base.setAttribute('stroke-opacity', '0.2');
            svg.append(base);
            values.forEach(function (v, i) {
                const height = Math.max(4, (v / max) * 100);
                const bar = document.createElementNS(ns, 'rect');
                bar.setAttribute('x', String(20 + i * slot + (slot - barWidth) / 2));
                bar.setAttribute('y', String(120 - height));
                bar.setAttribute('width', String(barWidth));
                bar.setAttribute('height', String(height));
                bar.setAttribute('rx', '8');
                bar.setAttribute('fill', 'currentColor');
                svg.append(bar);
            });
        }
        el.prepend(svg);
    });
});
