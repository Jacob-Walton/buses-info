export function initializeLegalNotices() {
	initializeLegalBanner();
	initializeLegalModal();
	initializeLegalNoticeHandlers();
}

function initializeLegalBanner() {
	const legalBanner = document.getElementById("legalBanner");
	const dismissButton = document.getElementById("dismissLegalBanner");

	if (!legalBanner || !dismissButton) return;

	if (!localStorage.getItem("legalBannerDismissed")) {
		legalBanner.style.display = "block";
		document.body.classList.add("has-banner");
	}

	dismissButton.addEventListener("click", () => {
		legalBanner.style.display = "none";
		document.body.classList.remove("has-banner");
		localStorage.setItem("legalBannerDismissed", "true");
	});
}

function initializeLegalModal() {
	const legalModal = document.getElementById("legalModal");
	const acknowledgeButton = document.getElementById("acknowledgeLegal");

	if (!legalModal || !acknowledgeButton) return;

	if (!localStorage.getItem("legalNoticeAccepted")) {
		showLegalModal();
	}
}

function initializeLegalNoticeHandlers() {
	document.getElementById("showLegalNotice")?.addEventListener("click", (e) => {
		e.preventDefault();
		showLegalModal();
	});

	document
		.getElementById("acknowledgeLegal")
		?.addEventListener("click", hideLegalModal);
	document.getElementById("legalModal")?.addEventListener("click", (e) => {
		if (e.target === e.currentTarget) {
			hideLegalModal();
		}
	});
}

function showLegalModal() {
	const legalModal = document.getElementById("legalModal");
	if (legalModal) {
		legalModal.style.display = "flex";
		document.body.style.overflow = "hidden";
	}
}

function hideLegalModal() {
	const legalModal = document.getElementById("legalModal");
	if (legalModal) {
		legalModal.style.display = "none";
		document.body.style.overflow = "";
		localStorage.setItem("legalNoticeAccepted", "true");
	}
}
