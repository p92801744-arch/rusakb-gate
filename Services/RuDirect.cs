namespace RusakbGate.Services;

/// <summary>
/// Трафик этих имён идёт напрямую, мимо VPS.
/// Суффиксы .ru / .su / .рф закрывают зону. Ниже — сервисы РФ, которые живут на .com / .net.
/// </summary>
public static class RuDirect
{
    public static readonly string[] Suffixes =
    [
        ".ru",
        ".su",
        ".xn--p1ai",

        // Bitrix / 1С. Зона .ru уже выше, здесь порталы на других окончаниях.
        "bitrix24.com",
        "bitrix24.eu",
        "bitrix24.de",
        "bitrix24.fr",
        "bitrix24.pl",
        "bitrix24.es",
        "bitrix24.in",
        "bitrix24.com.br",
        "bitrix24.site",
        "bitrix.info",
        "bitrixsoft.com",
        "bitrix24.net",
        "1cfresh.com",

        // VK
        "vk.com",
        "vk.me",
        "vk.cc",
        "userapi.com",
        "vkuser.net",
        "vkuseraudio.net",
        "vkuseraudio.com",
        "mvk.com",

        // Mail.ru за пределами .ru
        "my.com",

        // Яндекс за пределами .ru
        "yandex.com",
        "yandex.net",
        "yandexcloud.net",
        "yastatic.net",

        // Маркетплейсы, банки, карты
        "ozon.com",
        "ozonusercontent.com",
        "wbstatic.net",
        "sberbank.com",
        "tinkoff.com",
        "tbank.ru",
        "alfabank.com",
        "avito.st",
        "2gis.com"
    ];
}
