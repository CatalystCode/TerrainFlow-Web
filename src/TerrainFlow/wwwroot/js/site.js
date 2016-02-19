//File Upload response from the server
Dropzone.options.dropzoneForm = {
    init: function () {
        uploadMultiple: true,
        this.on("complete", function (data) {
            //var res = eval('(' + data.xhr.responseText + ')');
            var res = JSON.parse(data.xhr.responseText);
        });
    }
};