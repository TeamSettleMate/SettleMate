// =====================================================
// SETTLEMATE REGISTRATION JAVASCRIPT
// =====================================================


// =====================================================
// VARIABLES
// =====================================================

let currentStep = 1;

let otpTime = 120;

let otpTimerInterval = null;


// =====================================================
// INITIALIZE
// =====================================================

document.addEventListener("DOMContentLoaded", function () {

    updateUI();

    initializeIcons();

    initializeToasts();


    // -------------------------------------------------
    // OPEN OTP MODAL WHEN SERVER SAYS SHOW OTP
    // -------------------------------------------------

    if (
        window.settleMateRegistration &&
        window.settleMateRegistration.showOTP === true
    ) {

        currentStep = 4;

        updateUI();

        openModal();

    }

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
// UPDATE UI
// =====================================================

function updateUI() {


    // -------------------------------------------------
    // STEP CONTENT
    // -------------------------------------------------

    document
        .querySelectorAll(".step-content")
        .forEach(function (step) {

            step.classList.remove("active");

        });


    const currentStepElement =
        document.getElementById(
            "step-" + currentStep
        );


    if (currentStepElement) {

        currentStepElement.classList.add("active");

    }


    // -------------------------------------------------
    // STEP PILLS
    // -------------------------------------------------

    document
        .querySelectorAll(".step-pill")
        .forEach(function (pill, index) {

            const stepIndex = index + 1;


            if (stepIndex < currentStep) {

                pill.classList.add("completed");

                pill.classList.remove("active");

                pill.innerHTML =
                    '<i data-lucide="check"></i>';

            }

            else if (stepIndex === currentStep) {

                pill.classList.add("active");

                pill.classList.remove("completed");

                pill.innerHTML = stepIndex;

            }

            else {

                pill.classList.remove("active");

                pill.classList.remove("completed");

                pill.innerHTML = stepIndex;

            }

        });


    initializeIcons();


    // -------------------------------------------------
    // BACK BUTTON
    // -------------------------------------------------

    const backButton =
        document.getElementById("backBtn");


    if (backButton) {

        backButton.classList.toggle(
            "hidden",
            currentStep === 1
        );

    }


    // -------------------------------------------------
    // NEXT BUTTON
    // -------------------------------------------------

    const nextText =
        document.getElementById("nextText");


    const nextIcon =
        document.getElementById("nextIcon");


    if (!nextText || !nextIcon) {

        return;

    }


    if (currentStep === 4) {

        nextText.textContent =
            "Complete Registration";


        nextIcon.setAttribute(
            "data-lucide",
            "check-circle"
        );

    }

    else {

        nextText.textContent =
            "Continue";


        nextIcon.setAttribute(
            "data-lucide",
            "arrow-right"
        );

    }


    initializeIcons();

}


// =====================================================
// NEXT STEP
// =====================================================

function nextStep() {


    // =================================================
    // STEP 1 FRONTEND VALIDATION
    // =================================================

    if (currentStep === 1) {


        // -------------------------------------------------
        // FULL NAME
        // -------------------------------------------------

        const name =
            document.getElementById("txtname");


        if (
            !name ||
            name.value.trim() === ""
        ) {

            showToast(
                "error",
                "Something went wrong",
                "Please enter your full name."
            );


            if (name) {

                name.focus();

            }


            return;

        }


        // -------------------------------------------------
        // MOBILE NUMBER
        // -------------------------------------------------

        const phone =
            document.getElementById("phoneNumber");


        if (
            !phone ||
            phone.value.trim() === ""
        ) {

            showToast(
                "error",
                "Something went wrong",
                "Please enter your mobile number."
            );


            if (phone) {

                phone.focus();

            }


            return;

        }


        // -------------------------------------------------
        // MOBILE NUMBER FORMAT
        // -------------------------------------------------

        if (
            phone.value.trim().length !== 10 ||
            !/^[0-9]+$/.test(
                phone.value.trim()
            )
        ) {

            showToast(
                "error",
                "Something went wrong",
                "Please enter a valid 10-digit mobile number."
            );


            phone.focus();


            return;

        }


        // -------------------------------------------------
        // EMAIL
        // -------------------------------------------------

        const email =
            document.getElementById("emailInput");


        if (
            !email ||
            email.value.trim() === ""
        ) {

            showToast(
                "error",
                "Something went wrong",
                "Please enter your email address."
            );


            if (email) {

                email.focus();

            }


            return;

        }


        // -------------------------------------------------
        // EMAIL FORMAT
        // -------------------------------------------------

        const emailPattern =
            /^[^\s@]+@[^\s@]+\.[^\s@]+$/;


        if (
            !emailPattern.test(
                email.value.trim()
            )
        ) {

            showToast(
                "error",
                "Something went wrong",
                "Please enter a valid email address."
            );


            email.focus();


            return;

        }


        // -------------------------------------------------
        // ALL STEP 1 VALIDATION PASSED
        // -------------------------------------------------

        currentStep++;

        updateUI();

        return;

    }



    // =================================================
    // STEP 2 FRONTEND VALIDATION
    // =================================================

    if (currentStep === 2) {


        // -------------------------------------------------
        // PASSWORD
        // -------------------------------------------------

        const password =
            document.getElementById(
                "passwordInput"
            );


        if (
            !password ||
            password.value === ""
        ) {

            showToast(
                "error",
                "Something went wrong",
                "Please enter your password."
            );


            if (password) {

                password.focus();

            }


            return;

        }


        // -------------------------------------------------
        // CONFIRM PASSWORD
        // -------------------------------------------------

        const confirmPassword =
            document.getElementById(
                "confirmPasswordInput"
            );


        if (
            !confirmPassword ||
            confirmPassword.value === ""
        ) {

            showToast(
                "error",
                "Something went wrong",
                "Please confirm your password."
            );


            if (confirmPassword) {

                confirmPassword.focus();

            }


            return;

        }


        // -------------------------------------------------
        // PASSWORD MATCH
        // -------------------------------------------------

        if (
            password.value !==
            confirmPassword.value
        ) {

            showToast(
                "error",
                "Something went wrong",
                "Passwords do not match."
            );


            confirmPassword.focus();


            return;

        }


        // -------------------------------------------------
        // ALL STEP 2 VALIDATION PASSED
        // -------------------------------------------------

        currentStep++;

        updateUI();

        return;

    }



    // =================================================
    // STEP 3 FRONTEND VALIDATION
    // =================================================

    if (currentStep === 3) {


        // -------------------------------------------------
        // CITY
        // -------------------------------------------------

        const city =
            document.getElementById(
                "ddlcity"
            );


        if (
            !city ||
            city.value === ""
        ) {

            showToast(
                "error",
                "Something went wrong",
                "Please select your city."
            );


            if (city) {

                city.focus();

            }


            return;

        }


        // -------------------------------------------------
        // ADDRESS
        // -------------------------------------------------

        const address =
            document.getElementById(
                "address"
            );


        if (
            !address ||
            address.value.trim() === ""
        ) {

            showToast(
                "error",
                "Something went wrong",
                "Please enter your address."
            );


            if (address) {

                address.focus();

            }


            return;

        }


        // -------------------------------------------------
        // ALL STEP 3 VALIDATION PASSED
        // -------------------------------------------------

        currentStep++;

        updateUI();

        return;

    }



    // =================================================
    // STEP 4
    // =================================================

    if (currentStep === 4) {

        const form =
            document.getElementById(
                "regForm"
            );


        if (form) {

            form.submit();

        }

    }

}


// =====================================================
// PREVIOUS STEP
// =====================================================

function prevStep() {

    if (currentStep > 1) {

        currentStep--;

        updateUI();

    }

}


// =====================================================
// GENDER
// =====================================================

function selectGender(element) {


    document
        .querySelectorAll(".gender-pill")
        .forEach(function (pill) {

            pill.classList.remove(
                "selected"
            );

        });


    element.classList.add(
        "selected"
    );


    const genderInput =
        document.getElementById(
            "genderInput"
        );


    if (genderInput) {

        genderInput.value =
            element.innerText.trim();

    }

}


// =====================================================
// AVATAR
// =====================================================

function selectAvatar(element) {


    document
        .querySelectorAll(".avatar-card")
        .forEach(function (avatar) {

            avatar.classList.remove(
                "selected"
            );

        });


    element.classList.add(
        "selected"
    );


    const image =
        element.querySelector("img");


    if (!image) {

        return;

    }


    const imagePath =
        image.getAttribute("src");


    const fileName =
        imagePath.split("/").pop();


    const profilePhotoInput =
        document.getElementById(
            "profilePhotoInput"
        );


    if (profilePhotoInput) {

        profilePhotoInput.value =
            fileName;

    }

}


// =====================================================
// PASSWORD SHOW / HIDE
// =====================================================

function togglePassword(
    inputId,
    button
) {


    const input =
        document.getElementById(
            inputId
        );


    if (!input) {

        return;

    }


    const icon =
        button.querySelector("i");


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
// OPEN OTP MODAL
// =====================================================

function openModal() {


    const modal =
        document.getElementById(
            "verifyModal"
        );


    if (!modal) {

        return;

    }


    modal.classList.remove(
        "hidden"
    );


    document.body.classList.add(
        "modal-open"
    );


    requestAnimationFrame(function () {

        modal.classList.add(
            "show"
        );

    });


    startOTPTimer();


    setTimeout(function () {

        const otp1 =
            document.getElementById(
                "otp1"
            );


        if (otp1) {

            otp1.focus();

        }

    }, 250);

}


// =====================================================
// CLOSE OTP MODAL
// =====================================================

function closeModal() {


    const modal =
        document.getElementById(
            "verifyModal"
        );


    if (!modal) {

        return;

    }


    modal.classList.remove(
        "show"
    );


    document.body.classList.remove(
        "modal-open"
    );


    setTimeout(function () {

        modal.classList.add(
            "hidden"
        );

    }, 250);

}


// =====================================================
// START OTP TIMER
// =====================================================

function startOTPTimer() {


    otpTime = 120;


    if (otpTimerInterval !== null) {

        clearInterval(
            otpTimerInterval
        );

    }


    const timerElement =
        document.getElementById(
            "otpTimer"
        );


    if (!timerElement) {

        return;

    }


    timerElement.innerText =
        "02:00 remaining";


    otpTimerInterval =
        setInterval(function () {


            let minutes =
                Math.floor(
                    otpTime / 60
                );


            let seconds =
                otpTime % 60;


            timerElement.innerText =
                String(minutes)
                    .padStart(2, "0")
                + ":"
                + String(seconds)
                    .padStart(2, "0")
                + " remaining";


            otpTime--;


            if (otpTime < 0) {

                clearInterval(
                    otpTimerInterval
                );


                otpTimerInterval =
                    null;


                timerElement.innerText =
                    "OTP expired";

            }

        }, 1000);

}


// =====================================================
// MOVE OTP
// =====================================================

function moveOTP(
    current,
    nextId
) {


    current.value =
        current.value.replace(
            /[^0-9]/g,
            ""
        );


    if (
        current.value.length === 1 &&
        nextId
    ) {


        const nextInput =
            document.getElementById(
                nextId
            );


        if (nextInput) {

            nextInput.focus();

        }

    }

}


// =====================================================
// GET OTP
// =====================================================

function getOTP() {


    let otp = "";


    for (
        let i = 1;
        i <= 6;
        i++
    ) {


        const input =
            document.getElementById(
                "otp" + i
            );


        if (input) {

            otp += input.value;

        }

    }


    return otp;

}


// =====================================================
// VERIFY OTP
// =====================================================

function verifyOTP() {


    const otp =
        getOTP();


    if (otp.length !== 6) {

        showToast(
            "error",
            "Something went wrong",
            "Please enter the complete 6-digit verification code."
        );


        return;

    }


    const form =
        document.createElement(
            "form"
        );


    form.method =
        "POST";


    form.action =
        "/Student/Register/VerifyOTP";


    const input =
        document.createElement(
            "input"
        );


    input.type =
        "hidden";


    input.name =
        "otp";


    input.value =
        otp;


    form.appendChild(
        input
    );


    document.body.appendChild(
        form
    );


    form.submit();

}


// =====================================================
// RESEND OTP
// =====================================================

function resendOTP() {


    const form =
        document.createElement(
            "form"
        );


    form.method =
        "POST";


    form.action =
        "/Student/Register/ResendOTP";


    document.body.appendChild(
        form
    );


    form.submit();

}


// =====================================================
// TOAST
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
        document.createElement(
            "div"
        );


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


    container.appendChild(
        toast
    );


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
        document.createElement(
            "div"
        );


    div.textContent =
        value;


    return div.innerHTML;

}