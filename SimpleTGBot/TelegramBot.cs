using System.Reflection.Metadata.Ecma335;
using static System.Console;
using System.Text.RegularExpressions;

namespace SimpleTGBot;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

public class TelegramBot
{
    // Токен TG-бота.
    private const string BotToken = "8622644979:AAHzcHAXZ2SfMjEWsOnWp6KGNHAx8ozjFus";

    /// <summary>
    /// Инициализирует и обеспечивает работу бота до нажатия клавиши Esc
    /// </summary>
    public async Task Run()
    {
        // Если вам нужно хранить какие-то данные во время работы бота (массив информации, логи бота,
        // историю сообщений для каждого пользователя), то это всё надо инициализировать в этом методе.
        // TODO: Инициализация необходимых полей

        // Инициализируем наш клиент, передавая ему токен.
        var botClient = new TelegramBotClient(BotToken);

        // Служебные вещи для организации правильной работы с потоками
        using CancellationTokenSource cts = new CancellationTokenSource();

        // Разрешённые события, которые будет получать и обрабатывать наш бот.
        // Будем получать только сообщения. При желании можно поработать с другими событиями.
        ReceiverOptions receiverOptions = new ReceiverOptions()
        {
            AllowedUpdates = new[] { UpdateType.Message }
        };

        // Привязываем все обработчики и начинаем принимать сообщения для бота
        botClient.StartReceiving(
            updateHandler: OnMessageReceived,
            pollingErrorHandler: OnErrorOccured,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );

        // Проверяем что токен верный и получаем информацию о боте
        var me = await botClient.GetMeAsync(cancellationToken: cts.Token);
        WriteLine($"Бот @{me.Username} запущен.\nДля остановки нажмите клавишу Esc...");

        // Ждём, пока будет нажата клавиша Esc, тогда завершаем работу бота
        while (ReadKey().Key != ConsoleKey.Escape) { }

        // Отправляем запрос для остановки работы клиента.
        cts.Cancel();
    }

    /// <summary>
    /// реакция на команду /start. 
    /// </summary>
    /// <param name="botClient"></param>
    /// <param name="chatId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private async Task SendWelcomeMessage(ITelegramBotClient botClient, long chatId, CancellationToken ct)
    {
        string fileName = $"{chatId}.txt";

        if (System.IO.File.Exists(fileName))
        {
            string farmName = await System.IO.File.ReadAllTextAsync(fileName);
            await botClient.SendTextMessageAsync(chatId, $"С возвращением на ферму <b>{farmName}</b>!", parseMode: ParseMode.Html, cancellationToken: ct);
        }
        else
        {
            string welcomeText = "Добро пожаловать!\n\nЧтобы начать, дай своей ферме имя.\n\nНапиши сообщение в формате:\n<b>Моя ферма: Название</b>";
            await botClient.SendTextMessageAsync(chatId, welcomeText, parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    /// <summary>
    /// Обработчик события получения сообщения.
    /// </summary>
    /// <param name="botClient">Клиент, который получил сообщение</param>
    /// <param name="update">Событие, произошедшее в чате. Новое сообщение, голос в опросе, исключение из чата и т. д.</param>
    /// <param name="cancellationToken">Служебный токен для работы с многопоточностью</param>
    async Task OnMessageReceived(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message is null) return;

        var chatId = message.Chat.Id;
        string fileName = $"{chatId}.txt";

        string messageText = message.Text;
        WriteLine($"Получено сообщение в чате {chatId}: '{messageText}'");

        string command = messageText.ToLower().Trim();
        switch (command)
        {
            case "/start":
                await SendWelcomeMessage(botClient, chatId, cancellationToken);
                break;

            default:
                if (System.IO.File.Exists(fileName))
                {
                    string savedFarmName = await System.IO.File.ReadAllTextAsync(fileName);

                    if (command == "привет")
                        await botClient.SendTextMessageAsync(chatId, "Привет!");

                    else
                        await botClient.SendTextMessageAsync(chatId, "Команда принята! Скоро здесь появятся грядки.");
                }
                else
                {
                    var farmMatch = Regex.Match(messageText, @"^Моя ферма:\s*(.+)$", RegexOptions.IgnoreCase);

                    if (farmMatch.Success)
                    {
                        string farmName = farmMatch.Groups[1].Value;
                        await System.IO.File.WriteAllTextAsync(fileName, farmName);
                        await botClient.SendTextMessageAsync(chatId, $"Чудесно! Теперь твоя ферма официально называется <b>{farmName}</b>!", parseMode: ParseMode.Html);
                    }
                    else
                        await botClient.SendTextMessageAsync(chatId, "Прости, я не понимаю. Сначала дай ферме имя в формате:\n<b>Моя ферма: Название</b>", parseMode: ParseMode.Html);
                }
                break;
        }
    }


    /// <summary>
    /// Обработчик исключений, возникших при работе бота
    /// </summary>
    /// <param name="botClient">Клиент, для которого возникло исключение</param>
    /// <param name="exception">Возникшее исключение</param>
    /// <param name="cancellationToken">Служебный токен для работы с многопоточностью</param>
    /// <returns></returns>
    Task OnErrorOccured(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        // В зависимости от типа исключения печатаем различные сообщения об ошибке
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",

            _ => exception.ToString()
        };

        WriteLine(errorMessage);

        // Завершаем работу
        return Task.CompletedTask;
    }
}