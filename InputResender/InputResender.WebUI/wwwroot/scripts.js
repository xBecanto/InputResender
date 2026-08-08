function ApplyTheme() {
    src = document.getElementById("theme-selector").value;
    document.documentElement.setAttribute("data-theme", src);
}