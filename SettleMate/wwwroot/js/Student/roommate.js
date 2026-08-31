// =========================================================
// ROOMMATE MODULE
// =========================================================


// ---------------------------------------------------------
// LUCIDE ICONS
// ---------------------------------------------------------

document.addEventListener("DOMContentLoaded", function () {

    if (typeof lucide !== "undefined") {
        lucide.createIcons();
    }

});


// ---------------------------------------------------------
// DELETE CONFIRMATION
// ---------------------------------------------------------

function confirmDelete() {

    return confirm(
        "Are you sure you want to delete your roommate listing?"
    );

}