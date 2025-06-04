window.ScrollToBottom = function () {
    const element = document.getElementById("divChatCotent");
    element.scrollTop = element.scrollHeight;
}

window.HLLoad = function () {
    console.log("HLLoad Loading");
    hljs.highlightAll();
}