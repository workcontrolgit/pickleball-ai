window.initUploadDropZone = function (dropZoneId, inputId) {
    const dropZone = document.getElementById(dropZoneId);
    const input = document.getElementById(inputId);
    if (!dropZone || !input) return;

    dropZone.addEventListener('dragover', e => e.preventDefault());
    dropZone.addEventListener('drop', e => {
        e.preventDefault();
        if (!e.dataTransfer || e.dataTransfer.files.length === 0) return;
        const dt = new DataTransfer();
        for (const file of e.dataTransfer.files) dt.items.add(file);
        input.files = dt.files;
        input.dispatchEvent(new Event('change', { bubbles: true }));
    });
};
