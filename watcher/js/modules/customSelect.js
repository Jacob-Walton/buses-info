export function initializeCustomSelects() {
    const customSelects = document.querySelectorAll(".custom-select");

    customSelects.forEach(initializeSelect);

    document.addEventListener("click", closeAllSelect);
}

function initializeSelect(customSelect) {
    const selectSelected = customSelect.querySelector(".select-selected");
    const selectItems = customSelect.querySelector(".select-items");

    if (!selectSelected || !selectItems) return;

    selectSelected.addEventListener("click", (e) => {
        e.stopPropagation();
        closeAllSelect(selectSelected);
        selectItems.classList.toggle("select-hide");
        selectSelected.classList.toggle("select-arrow-active");
    });

    initializeOptions(selectItems, selectSelected, customSelect);
}

function initializeOptions(selectItems, selectSelected, customSelect) {
    selectItems.querySelectorAll("div").forEach(option => {
        option.addEventListener("click", function(e) {
            e.stopPropagation();
            updateSelection(this, selectSelected, customSelect);
        });
    });
}

function updateSelection(option, selectSelected, customSelect) {
    selectSelected.textContent = option.textContent;
    const hiddenInput = customSelect.querySelector('input[type="hidden"]');
    if (hiddenInput) {
        hiddenInput.value = option.dataset.value;
        hiddenInput.dispatchEvent(new Event("change", { bubbles: true }));
    }
    closeSelect(selectSelected);
}

function closeSelect(selectSelected) {
    const selectItems = selectSelected.nextElementSibling;
    selectItems.classList.add("select-hide");
    selectSelected.classList.remove("select-arrow-active");
}

function closeAllSelect(element) {
    document.querySelectorAll(".custom-select").forEach(select => {
        const selected = select.querySelector(".select-selected");
        const items = select.querySelector(".select-items");
        if (selected && items && selected !== element) {
            items.classList.add("select-hide");
            selected.classList.remove("select-arrow-active");
        }
    });
}
