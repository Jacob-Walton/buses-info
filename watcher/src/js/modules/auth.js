export function initializeAuth() {
	const logoutButton = document.getElementById("logoutButton");
	if (logoutButton) {
		logoutButton.addEventListener("click", handleLogout);
	}
}

async function handleLogout() {
	try {
		const response = await fetch("/logout", { method: "POST" });
		if (response.ok) {
			window.location.href = "/";
		} else {
			console.error("Failed to log out");
		}
	} catch (error) {
		console.error(error);
	}
}
