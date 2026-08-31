// =====================================================
// SETTLEMATE LOGIN JAVASCRIPT
// =====================================================


// =====================================================
// INITIALIZE
// =====================================================

document.addEventListener("DOMContentLoaded", function () {

    initializeIcons();

    initializeToasts();

    initializeLoginValidation();

});


// =====================================================
// LUCIDE ICONS
// =====================================================

function initializeIcons() {

    if (
        typeof lucide !== "undefined" &&
        lucide.createIcons
    ) {

        lucide.createIcons();

    }

}


// =====================================================
// PASSWORD SHOW / HIDE
// =====================================================

function togglePassword(inputId, button) {

    const input =
        document.getElementById(inputId);


    if (!input) {
        return;
    }


    const icon =
        button.querySelector("i");


    if (!icon) {
        return;
    }


    if (input.type === "password") {

        input.type = "text";


        icon.setAttribute(
            "data-lucide",
            "eye"
        );

    }

    else {

        input.type = "password";


        icon.setAttribute(
            "data-lucide",
            "eye-off"
        );

    }


    initializeIcons();

}


// =====================================================
// LOGIN FRONTEND VALIDATION
// =====================================================

function initializeLoginValidation() {

    const form =
        document.getElementById("loginForm");


    if (!form) {
        return;
    }


    form.addEventListener(
        "submit",
        function (event) {

            event.preventDefault();


            const email =
                document.getElementById("emailInput");


            const password =
                document.getElementById("passwordInput");


            // ---------------------------------------------
            // EMAIL VALIDATION
            // ---------------------------------------------

            if (!email) {
                return;
            }


            if (email.value.trim() === "") {

                showToast(
                    "error",
                    "Something went wrong",
                    "Please enter your email address."
                );

                email.focus();

                return;

            }


            if (!isValidEmail(email.value.trim())) {

                showToast(
                    "error",
                    "Something went wrong",
                    "Please enter a valid email address."
                );

                email.focus();

                return;

            }


            // ---------------------------------------------
            // PASSWORD VALIDATION
            // ---------------------------------------------

            if (!password) {
                return;
            }


            if (password.value.trim() === "") {

                showToast(
                    "error",
                    "Something went wrong",
                    "Please enter your password."
                );

                password.focus();

                return;

            }


            // ---------------------------------------------
            // FRONTEND VALIDATION PASSED
            // ---------------------------------------------

            form.submit();

        }
    );

}


// =====================================================
// EMAIL VALIDATION
// =====================================================

function isValidEmail(email) {

    const emailPattern =
        /^[^\s@]+@[^\s@]+\.[^\s@]+$/;


    return emailPattern.test(email);

}


// =====================================================
// SHOW TOAST
// =====================================================

function showToast(
    type,
    title,
    message
) {

    const container =
        document.getElementById(
            "toastContainer"
        );


    if (!container) {
        return;
    }


    const toast =
        document.createElement("div");


    toast.className =
        "settlemate-toast " +
        (
            type === "success"
                ? "success-toast"
                : "error-toast"
        );


    const iconName =
        type === "success"
            ? "check"
            : "x";


    toast.innerHTML = `

        <div class="toast-icon">

            <i data-lucide="${iconName}"></i>

        </div>


        <div class="toast-content">

            <strong>
                ${escapeHTML(title)}
            </strong>

            <p>
                ${escapeHTML(message)}
            </p>

        </div>


        <button type="button"
                class="toast-close"
                aria-label="Close notification"
                onclick="closeToast(this)">

            <i data-lucide="x"></i>

        </button>

    `;


    container.appendChild(toast);


    initializeIcons();


    setTimeout(function () {

        toast.classList.add(
            "toast-hide"
        );


        setTimeout(function () {

            toast.remove();

        }, 300);


    }, 6000);

}


// =====================================================
// CLOSE TOAST
// =====================================================

function closeToast(button) {

    const toast =
        button.closest(
            ".settlemate-toast"
        );


    if (!toast) {
        return;
    }


    toast.classList.add(
        "toast-hide"
    );


    setTimeout(function () {

        toast.remove();

    }, 300);

}


// =====================================================
// INITIALIZE SERVER TOASTS
// =====================================================

function initializeToasts() {

    const toasts =
        document.querySelectorAll(
            ".settlemate-toast"
        );


    toasts.forEach(function (toast) {

        setTimeout(function () {

            toast.classList.add(
                "toast-hide"
            );


            setTimeout(function () {

                toast.remove();

            }, 300);


        }, 6000);

    });

}


// =====================================================
// ESCAPE HTML
// =====================================================

function escapeHTML(value) {

    const div =
        document.createElement("div");


    div.textContent =
        value;


    return div.innerHTML;

}