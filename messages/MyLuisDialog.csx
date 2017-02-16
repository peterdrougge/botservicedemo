#load "Message.csx"
using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

using Microsoft.Bot.Connector;
using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;

using Microsoft.WindowsAzure.Storage; 
using Microsoft.WindowsAzure.Storage.Queue; 

[Serializable]
public class MyLuisDialog : LuisDialog<object>
{
    protected int count = 1;

    [NonSerialized]
    protected IMessageActivity incomingMessage;

    public MyLuisDialog() : base(new LuisService(new LuisModelAttribute(Utils.GetAppSetting("LuisAppId"), Utils.GetAppSetting("LuisAPIKey"))))
    {
    }

    protected override async Task MessageReceived(IDialogContext context, IAwaitable<IMessageActivity> item)
    {
        this.incomingMessage = await item;
        await base.MessageReceived(context, item);
    }
    [LuisIntent("None")]
    public async Task NoneIntent(IDialogContext context, LuisResult result)
    {
        if (incomingMessage.Text == "reset")
        {
            PromptDialog.Confirm(
                context,
                AfterResetAsync,
                "Are you sure you want to reset the count?",
                "Didn't get that!",
                promptStyle: PromptStyle.Auto);
        }
        else
        {
            await context.PostAsync($"{this.count++}: You have reached the none intent. You said: {result.Query}"); //
            context.Wait(MessageReceived);
        }
    }

    [LuisIntent("AddToQueue")]
    public async Task AddToQueue(IDialogContext context, LuisResult result)
    {
        // Create a queue Message
        var queueMessage = new Message
        {
            ResumptionCookie = new ResumptionCookie(incomingMessage),
            Text = incomingMessage.Text
        };

        // write the queue Message to the queue
        await AddMessageToQueueAsync(JsonConvert.SerializeObject(queueMessage));

        await context.PostAsync($"{this.count++}: You said {queueMessage.Text}. Message added to the queue.");
        context.Wait(MessageReceived);
    }
    [LuisIntent("GetStockPrice")]
    public async Task GetStockData(IDialogContext context, LuisResult result)
    {
        EntityRecommendation company;
        if (result.TryFindEntity("Company", out company))
        {
            context.UserData.SetValue<string>("Company", company.Entity);
            await context.PostAsync($"You have reached the stockprice intent for {company.Entity}. You said: {result.Query}"); 
        }
        else
        {
            await context.PostAsync($"You have reached the stockprice intent. You said: {result.Query}");
        }
        
        context.Wait(MessageReceived);
    }
    [LuisIntent("GetStockPriceAgain")]
    public async Task GetStockDataAgain(IDialogContext context, LuisResult result)
    {
        var company = string.Empty;
        context.UserData.TryGetValue<string>("Company", out company);
        await context.PostAsync($"You have reached the stockpriceagain intent for {company}. You said: {result.Query}"); 
        context.Wait(MessageReceived);
    }

    [LuisIntent("GetTime")]
    public async Task GetTime(IDialogContext context, LuisResult result)
    {
        await context.PostAsync($"It's {DateTime.Now.ToString()}"); //
        context.Wait(MessageReceived);
    }

    [LuisIntent("GetWeather")]
    public async Task GetWeather(IDialogContext context, LuisResult result)
    {
        EntityRecommendation city;
        if (result.TryFindEntity("builtin.geography.city", out city))
        {
            await context.PostAsync($"You have reached the weather intent for {city.Entity}. You said: {result.Query}"); 
        }
        else
        {
            await context.PostAsync($"You have reached the weather intent. You said: {result.Query}"); 
        }
        context.Wait(MessageReceived);
    }

    public static async Task AddMessageToQueueAsync(string message)
    {
        // Retrieve storage account from connection string.
        var storageAccount = CloudStorageAccount.Parse(Utils.GetAppSetting("AzureWebJobsStorage"));

        // Create the queue client.
        var queueClient = storageAccount.CreateCloudQueueClient();

        // Retrieve a reference to a queue.
        var queue = queueClient.GetQueueReference("bot-queue");

        // Create the queue if it doesn't already exist.
        await queue.CreateIfNotExistsAsync();
        
        // Create a message and add it to the queue.
        var queuemessage = new CloudQueueMessage(message);
        await queue.AddMessageAsync(queuemessage);
    }

    public async Task AfterResetAsync(IDialogContext context, IAwaitable<bool> argument)
    {
        var confirm = await argument;
        if (confirm)
        {
            this.count = 1;
            await context.PostAsync("Reset count.");
        }
        else
        {
            await context.PostAsync("Did not reset count.");
        }
        context.Wait(MessageReceived);
    }
}