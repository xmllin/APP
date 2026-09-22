using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace Nexora.Services
{
    public enum NotificationKind
    {
        Info,
        Success,
        Warning,
        Error
    }

    public static class NotificationFormatter
    {
        public static string FormatDownloadError(Exception exception)
        {
            if (exception == null)
                return "Неизвестная ошибка загрузки.";

            if (exception is OperationCanceledException)
                return "Загрузка отменена.";

            if (exception is UnauthorizedAccessException)
                return "Нет доступа к папке загрузок. Выберите другую папку в настройках.";

            if (exception is DirectoryNotFoundException)
                return "Папка загрузок не найдена. Выберите другую папку в настройках.";

            if (exception is DriveNotFoundException)
                return "Диск для загрузок недоступен.";

            if (exception is IOException)
            {
                if ((exception.Message ?? string.Empty).Contains("Недостаточно места", StringComparison.OrdinalIgnoreCase))
                    return "Недостаточно места на диске для сохранения файла.";
                return "Не удалось записать файл на диск. Проверьте свободное место и права доступа.";
            }

            if (exception is HttpRequestException http)
            {
                if (http.StatusCode.HasValue)
                {
                    var code = (int)http.StatusCode.Value;
                    switch (code)
                    {
                        case 403:
                            return "Сервер запретил загрузку (403). Для этого источника может потребоваться VPN или дополнительный доступ.";
                        case 404:
                            return "Файл загрузки не найден (404). Возможно, ссылка устарела.";
                        case 408:
                            return "Сервер не дождался ответа (408). Попробуйте ещё раз.";
                        case 429:
                            return "Слишком много запросов (429). Подождите немного и повторите попытку.";
                        default:
                            if (code >= 500 && code <= 599)
                                return "Сервер временно недоступен (" + code + "). Попробуйте ещё раз позже.";
                            return "Сервер вернул ошибку " + code + ".";
                    }
                }

                return "Не удалось подключиться к серверу. Проверьте интернет-соединение, VPN или прокси.";
            }

            if (exception is JsonException)
                return "Сервер вернул некорректные данные. Повторите попытку позже.";

            if (exception is InvalidOperationException)
            {
                var message = exception.Message ?? string.Empty;
                if (message.Contains("веб-страницу", StringComparison.OrdinalIgnoreCase))
                    return "Источник вернул веб-страницу вместо файла. Ссылка загрузки недействительна или требует дополнительного доступа.";
                if (message.Contains("прямой файл", StringComparison.OrdinalIgnoreCase))
                    return "Не удалось найти прямой файл загрузки на официальном источнике.";
                if (message.Contains("Windows-файл", StringComparison.OrdinalIgnoreCase))
                    return "Для этой программы не найден подходящий файл для Windows.";
                return message;
            }

            if (exception is TimeoutException)
                return "Время ожидания загрузки истекло. Проверьте соединение и повторите попытку.";

            var inner = exception.InnerException;
            if (inner != null && !ReferenceEquals(inner, exception))
            {
                var innerMessage = FormatDownloadError(inner);
                if (!string.IsNullOrWhiteSpace(innerMessage) && innerMessage != "Неизвестная ошибка загрузки.")
                    return innerMessage;
            }

            return "Не удалось выполнить загрузку. Попробуйте ещё раз.";
        }

        public static string FormatGeneralError(Exception exception)
        {
            if (exception == null)
                return "Произошла неизвестная ошибка.";
            if (exception is OperationCanceledException)
                return "Операция отменена.";
            if (exception is UnauthorizedAccessException)
                return "Нет доступа к файлу или папке.";
            if (exception is DirectoryNotFoundException)
                return "Файл или папка не найдены.";
            if (exception is DriveNotFoundException)
                return "Необходимый диск недоступен.";
            if (exception is IOException)
                return "Не удалось выполнить операцию с файлом. Проверьте диск и права доступа.";
            if (exception is JsonException)
                return "Не удалось прочитать данные каталога. Файл данных имеет неверный формат.";
            if (exception is HttpRequestException)
                return FormatDownloadError(exception);
            return string.IsNullOrWhiteSpace(exception.Message)
                ? "Произошла неизвестная ошибка."
                : exception.Message;
        }
    }
}
