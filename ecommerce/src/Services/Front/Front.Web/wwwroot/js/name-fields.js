document.querySelectorAll('[data-name-field]').forEach(input => {
    input.addEventListener('paste', event => event.preventDefault());
    input.addEventListener('beforeinput', event => {
        if (event.data && !/^[\p{L}\p{M} ]+$/u.test(event.data)) event.preventDefault();
    });
});
