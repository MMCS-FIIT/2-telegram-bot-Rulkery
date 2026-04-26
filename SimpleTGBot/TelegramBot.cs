using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using static System.Console;

namespace SimpleTGBot;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class TelegramBot
{
    // Токен TG-бота.
    private const string BotToken = "8622644979:AAHzcHAXZ2SfMjEWsOnWp6KGNHAx8ozjFus";

    private string[] _farmsSeeds = new[] { "Морковь", "Тыква", "Редис", "Томат", "Свекла" };

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
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }

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
            var replyKeyboard = new ReplyKeyboardMarkup(new[]{new [] {
            new KeyboardButton("Посадить"),
            new KeyboardButton("Статус")}})
            { ResizeKeyboard = true };

            var farmName = (await System.IO.File.ReadAllTextAsync(fileName)).Split(';')[0];

            await botClient.SendTextMessageAsync(chatId, $"С возвращением на ферму <b>{farmName}</b>!", parseMode: ParseMode.Html, replyMarkup: replyKeyboard, cancellationToken: ct);
        }
        else
        {
            var removeKeyboard = new ReplyKeyboardRemove();
            string welcomeText = "Добро пожаловать!\n\nЧтобы начать, дай своей ферме имя.\n\nНапиши сообщение в формате:\n<b>Моя ферма: Название</b>";
            await botClient.SendTextMessageAsync(chatId, welcomeText, parseMode: ParseMode.Html, replyMarkup: removeKeyboard, cancellationToken: ct);
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
        if (update.Type == UpdateType.CallbackQuery)
        {
            var callbackQuery = update.CallbackQuery;
            if (callbackQuery.Message is null) return;

            var chatId = callbackQuery.Message.Chat.Id;
            var data = callbackQuery.Data;

            if (data != null && data.StartsWith("plant_"))
            {
                string chosenPlant = data.Replace("plant_", "");
                string fileName = $"{chatId}.txt";

                if (System.IO.File.Exists(fileName))
                {
                    string fileContent = await System.IO.File.ReadAllTextAsync(fileName);
                    string[] parts = fileContent.Split(';');

                    string myPlant = parts[1];
                    int count = int.Parse(parts[2]);

                    if (chosenPlant != myPlant || count <= 0)
                    {
                        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, $"У тебя нет семян вида {chosenPlant}!", showAlert: true);
                        return;
                    }

                    if (count > 0)
                    {
                        count--;
                        await System.IO.File.WriteAllTextAsync(fileName, $"{parts[0]};{myPlant};{count}");

                        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, $"Посажено: {chosenPlant}!");
                        await botClient.SendTextMessageAsync(chatId, $"Ты посадил <b>{chosenPlant}</b>. Осталось семян этого вида: {count}", parseMode: ParseMode.Html);
                    }
                }
            }
            return;
        }

        var message = update.Message;
        if (message is null || message.Text is null) return;

        long currentChatId = message.Chat.Id;
        var currentFileName = $"{currentChatId}.txt";
        var sessionFile = $"{currentChatId}.session";

        string messageText = message.Text;
        WriteLine($"Получено сообщение в чате {currentChatId}: '{messageText}'");

        string command = messageText.ToLower().Trim();
        switch (command)
        {
            case "/start":
                if (!System.IO.File.Exists(sessionFile))
                    await System.IO.File.WriteAllTextAsync(sessionFile, "started");
                await SendWelcomeMessage(botClient, currentChatId, cancellationToken);
                break;

            default:
                if (!System.IO.File.Exists(sessionFile))
                {
                    await botClient.SendTextMessageAsync(
                        chatId: currentChatId,
                        text: "Привет! Мы еще не знакомы. Напиши команду <b>/start</b>, чтобы начать!",
                        parseMode: ParseMode.Html,
                        replyMarkup: new ReplyKeyboardRemove());
                }

                else if (!System.IO.File.Exists(currentFileName))
                {
                    var farmMatch = Regex.Match(messageText, @"^Моя ферма:\s*(.+)$", RegexOptions.IgnoreCase);

                    if (farmMatch.Success)
                    {
                        string farmName = farmMatch.Groups[1].Value;
                        var r = new Random();
                        var startSeeds = $"{_farmsSeeds[r.Next(_farmsSeeds.Length)]}";
                        var startSeedsCount = 5;

                        var replyKeyboard = new ReplyKeyboardMarkup(new[]{new [] {
                new KeyboardButton("Посадить"),
                new KeyboardButton("Статус")}})
                        { ResizeKeyboard = true };

                        await System.IO.File.WriteAllTextAsync(currentFileName, $"{farmName};{startSeeds};{startSeedsCount}");

                        await botClient.SendTextMessageAsync(currentChatId, $"Чудесно! Теперь твоя ферма официально называется <b>{farmName}</b>!", parseMode: ParseMode.Html);
                        await botClient.SendTextMessageAsync(currentChatId, $"Бывший владелец фермы припас для тебя немного семян...");
                        await botClient.SendTextMessageAsync(currentChatId, text: $"Это же {startSeeds}! Давай скорее посадим!", replyMarkup: replyKeyboard, cancellationToken: cancellationToken);
                    }
                    else
                        // Если сессия есть,названия нет
                        await botClient.SendTextMessageAsync(chatId: currentChatId,
                            text: "Сначала надо назвать ферму! Напиши:\n<b>Моя ферма: Название</b>", parseMode: ParseMode.Html,
                            replyMarkup: new ReplyKeyboardRemove());
                }


                else
                {
                    string[] plantSynonyms = { "посадить", "да", "сажать", "давай" };

                    if (plantSynonyms.Contains(command))
                    {
                        var inlineKeyboard = new InlineKeyboardMarkup(new[]
                        {_farmsSeeds.Select(seed => InlineKeyboardButton.WithCallbackData(seed, $"plant_{seed}")).ToArray()});

                        await botClient.SendTextMessageAsync(currentChatId,
                            "Выбери, что именно ты хочешь посадить на грядку:",
                            replyMarkup: inlineKeyboard);
                    }
                    else if (command == "статус")
                    {
                        string fileContent = await System.IO.File.ReadAllTextAsync(currentFileName);
                        var parts = fileContent.Split(';');
                        await botClient.SendTextMessageAsync(currentChatId,
                            $"<b>Ферма:</b> {parts[0]}\n<b>Запас:</b> {parts[1]} ({parts[2]} шт.)",
                            parseMode: ParseMode.Html);
                    }

                    else if (command == "привет")
                        await botClient.SendTextMessageAsync(currentChatId, "Привет!");
                    else
                        await botClient.SendTextMessageAsync(currentChatId, "Команда принята! Скоро здесь появятся грядки.");
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