// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

function setButtonLoading(button, loadingText) {
    if (!button) return;
    button.dataset.originalText = button.innerHTML;
    button.innerHTML = `<span class='spinner-border spinner-border-sm me-2' role='status' aria-hidden='true'></span>${loadingText}`;
    button.disabled = true;
    button.classList.add('loading');
}

function unsetButtonLoading(button) {
    if (!button) return;
    button.innerHTML = button.dataset.originalText || 'Submit';
    button.disabled = false;
    button.classList.remove('loading');
}
