window.initUploadDropZone = function (dropZoneId, inputId, dotNetRef) {
    const dropZone = document.getElementById(dropZoneId);
    const input = document.getElementById(inputId);
    if (!dropZone || !input) return;

    // Click anywhere on drop zone → open file picker
    dropZone.addEventListener('click', () => input.click());

    // Drag styling
    dropZone.addEventListener('dragenter', e => {
        e.preventDefault();
        e.stopPropagation();
        dropZone.classList.add('dragging');
        dotNetRef.invokeMethodAsync('SetDragging', true);
    });

    dropZone.addEventListener('dragover', e => {
        e.preventDefault();
        e.stopPropagation();
    });

    dropZone.addEventListener('dragleave', e => {
        e.preventDefault();
        e.stopPropagation();
        // Only leave if we've left the drop zone entirely
        if (!dropZone.contains(e.relatedTarget)) {
            dropZone.classList.remove('dragging');
            dotNetRef.invokeMethodAsync('SetDragging', false);
        }
    });

    // Drop → forward files to the hidden input → trigger Blazor change
    dropZone.addEventListener('drop', e => {
        e.preventDefault();
        e.stopPropagation();
        dropZone.classList.remove('dragging');
        dotNetRef.invokeMethodAsync('SetDragging', false);

        const files = e.dataTransfer && e.dataTransfer.files;
        if (!files || files.length === 0) return;

        const dt = new DataTransfer();
        dt.items.add(files[0]); // single file
        input.files = dt.files;
        input.dispatchEvent(new Event('change', { bubbles: true }));
    });
};
