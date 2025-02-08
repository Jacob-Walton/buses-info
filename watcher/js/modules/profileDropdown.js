export function initializeProfileDropdown() {
    const profileTrigger = document.getElementById("profileTrigger");
    const profileDropdown = document.getElementById("profileDropdown");

    if (!profileTrigger || !profileDropdown) return;

    profileTrigger.addEventListener("click", (e) => {
        e.stopPropagation();
        profileDropdown.classList.toggle("active");
    });

    document.addEventListener("click", (event) => {
        if (!profileDropdown.contains(event.target)) {
            profileDropdown.classList.remove("active");
        }
    });
}
