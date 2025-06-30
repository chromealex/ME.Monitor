mergeInto(LibraryManager.library, {
  ping: function (address) {
    var xhr = new XMLHttpRequest();
    xhr.open("HEAD", Pointer_stringify(address));
    xhr.onreadystatechange = function() {
      if (xhr.readyState == 4)
        SendMessage(address, "Receive", Date.now() - this.time);
    };
    xhr.time = Date.now();
    xhr.send();
  },
});