window.blazorModal = {
    show: (element) => {
        const modal = bootstrap.Modal.getOrCreateInstance(element, {
            backdrop: 'static',
            keyboard: false
        });
        modal.show();
    },
    hide: (element) => {
        const modal = bootstrap.Modal.getOrCreateInstance(element);
        modal.hide();
    }
};
