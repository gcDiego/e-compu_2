document.querySelectorAll('[data-name-field]').forEach(input => {
    input.addEventListener('paste', event => event.preventDefault());
    input.addEventListener('beforeinput', event => {
        const allowed = input.hasAttribute('data-allow-numbers') ? /^[\p{L}\p{M}\p{N} ]+$/u : /^[\p{L}\p{M} ]+$/u;
        if (event.data && !allowed.test(event.data)) event.preventDefault();
    });
});
