$(function () {
    var url = location.pathname;
    $('#navbar ul>li a').each(function () {
        if (url == $(this).attr("href")) {
            $(this).addClass("active");
        } else {
            $(this).removeClass("active")
        }
    });
});