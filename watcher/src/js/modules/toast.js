class ToastManager {
	constructor() {
		this.createToastContainer();
		this.toasts = [];
		this.maxToasts = 5;
		this.height = 64; // Height of each toast + margin
	}

	createToastContainer() {
		const container = document.createElement("div");
		container.id = "toast-container";
		document.body.appendChild(container);
	}

	show(message, type = "success", options = {}) {
		const id = Math.random().toString(36).substr(2, 9);
		const toast = this.createToastElement(message, type, options);
		toast.dataset.id = id;

		const container = document.getElementById("toast-container");
		container.appendChild(toast);

		this.toasts.push({ element: toast, id });

		toast.style.transform = `translateY(${this.height}px) scale(0.9)`;
		toast.style.opacity = "0";

		requestAnimationFrame(() => {
			this.updateToastStack();
		});

		if (!options.persistent) {
			setTimeout(() => {
				this.dismiss(id);
			}, options.duration || 4000);
		}

		return id;
	}

	updateToastStack() {
		const maxVisible = Math.min(this.toasts.length, this.maxToasts);

		this.toasts.forEach((toast, index) => {
			if (index < maxVisible) {
				const scale = 1 - index * 0.05;
				const y = index * (this.height / 2); // Compressed spacing
				const opacity = 1 - index * 0.15;

				toast.element.style.transition =
					"all 0.3s cubic-bezier(0.16, 1, 0.3, 1)";
				toast.element.style.transform = `translateY(-${y}px) scale(${scale})`;
				toast.element.style.opacity = opacity.toString();
				toast.element.style.zIndex = (1000 - index).toString();
			} else {
				// Hide extra toasts
				toast.element.style.opacity = "0";
				toast.element.style.transform = "translateY(0) scale(0.9)";
			}
		});
	}

	dismiss(id) {
		const toast = this.toasts.find((t) => t.id === id);
		if (!toast) return;

		const { element } = toast;
		element.style.transition = "all 0.2s cubic-bezier(0.16, 1, 0.3, 1)";
		element.style.transform = "translateX(calc(100% + 20px))";
		element.style.opacity = "0";

		setTimeout(() => {
			element.remove();
			this.toasts = this.toasts.filter((t) => t.id !== id);
			this.updateToastStack();
		}, 200);
	}

	updateToastPositions() {
		this.toasts.forEach((toast, index) => {
			const offset = index * 64; // Height of toast + gap
			toast.element.style.transform = `translateY(-${offset}px)`;
		});
	}

	createToastElement(message, type, options) {
		const toast = document.createElement("div");
		toast.className = `toast toast-${type}`;

		if (options.loading) {
			const spinner = document.createElement("div");
			spinner.className = "toast-spinner";
			toast.appendChild(spinner);
		} else {
			const icon = document.createElement("i");
			icon.className = this.getIconClass(type);
			toast.appendChild(icon);
		}

		const content = document.createElement("div");
		content.className = "toast-content";

		const title = document.createElement("p");
		title.className = "toast-title";
		title.textContent = options.title || message;

		content.appendChild(title);

		if (options.title) {
			const description = document.createElement("p");
			description.className = "toast-description";
			description.textContent = message;
			content.appendChild(description);
		}

		toast.appendChild(content);

		if (options.action) {
			const button = document.createElement("button");
			button.className = "toast-action";
			button.textContent = options.action.label;
			button.onclick = options.action.onClick;
			toast.appendChild(button);
		}

		const closeButton = document.createElement("button");
		closeButton.className = "toast-close";
		closeButton.innerHTML = "×";
		closeButton.onclick = () => this.dismiss(toast.dataset.id);
		toast.appendChild(closeButton);

		return toast;
	}

	getIconClass(type) {
		switch (type) {
			case "success":
				return "fas fa-check-circle";
			case "error":
				return "fas fa-exclamation-circle";
			case "warning":
				return "fas fa-exclamation-triangle";
			default:
				return "fas fa-info-circle";
		}
	}
}

export const toast = new ToastManager();
