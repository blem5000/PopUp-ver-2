Program do wyświetlania pop up'a jednorazowo u użytkowników, którzy nacisneli ok. Dla testów należy użyć jak poniżej podmieniając odpowiednio ścieżki:
sciezka_do_exe -PopupId "TEST" -RtfPath "sciezka_do_pliku_rtf"
do używania u użytkowników należy podać również inną flagę popupid gdyż TEST nie zapisuje flagi na dysku.

plik towarzyszący .ps1 umożliwia uruchomienie popup'u u użytkowników za pomocą task scheduler'a. Należy zmienić zmienną $sourceDir na odpowiednią lokalizację z plikami rtf i exe. Do uruchomienia należy dodać argumenty

PopupId: IT_2026_06_03
Deadline: 2026-06-10 23:59
ClearFlag: false

dla testów ustawić ClearFlag na true
