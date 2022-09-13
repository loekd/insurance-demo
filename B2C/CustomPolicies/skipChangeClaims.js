ready(function () {
  skipChangeClaimsPage();
});

function skipChangeClaimsPage() {
  if (!MutationObserver) {
    return;
  }
  var changeClaimsLink = document.querySelector(
    'button[id$="_but_change_claims"]'
  );
  var nextButton = document.getElementById("continue");
  if (changeClaimsLink && nextButton) {
    const config = { attributes: true };
    const callback = function (mutationsList, observer) {
      //see if the link became visible, if so, we'll click next to skip a useless screen
      for (const mutation of mutationsList) {
        if (
          mutation.type === "attributes" &&
          mutation.attributeName === "style" &&
          mutation.target.attributes["style"].nodeValue === "display: inline;"
        ) {
          nextButton.click();
        }
      }
    };
    const observer = new MutationObserver(callback);
    observer.observe(changeClaimsLink, config);
  }
}

function ready(callbackFunc) {
  if (document.readyState !== "loading") {
    // Document is already ready, call the callback directly
    callbackFunc();
  } else if (document.addEventListener) {
    // All modern browsers to register DOMContentLoaded
    document.addEventListener("DOMContentLoaded", callbackFunc);
  } else {
    // Old IE browsers
    document.attachEvent("onreadystatechange", function () {
      if (document.readyState === "complete") {
        callbackFunc();
      }
    });
  }
}
