<h1 align="center"> TelegramBotWebhook </h1>
<h3 align="center"> <a href="https://github.com/TelegramBots/telegram.bot">Telegram.Bot</a> wrapper with webhook over <a href="https://ngrok.com/">ngrok</a> </h3>

## Table of Contents

- [Introduction](#introduction)
- [Features](#features)
- [Usage](#usage)
- [Feedback](#feedback)


## Introduction

TelegramBotWebhook is inherited class from Telegram.Bot.TelegramBotClient, allows you to create a bot with webhook support without a real IP address.


## Features

* Starting ngrok in background.
* Retrieving proxy from <a href="https://getproxylist.com/">getproxylist.com</a>.
* Testing proxy on <a href="https://telegram.org/">telegram.org</a> availability.


## Usage

#### VB.NET

```VB
'Declaration
Public WithEvents Bot As TelegramBotWebhook

...

'Retrieving proxy include EN, DE, exclude RU countries
Dim proxy = Await TelegramBotWebhook.GetWebProxy({"EN", "DE"}, {"RU"})

If proxy IsNot Nothing Then

    'Starting Bot with retrieved proxy (already tested)
    Bot = New TelegramBotWebhook(MyTelegramToken, proxy, MyNgrokToken)

Else

    Dim MyProxy = New Net.WebProxy(MyProxyAddress, MyProxyPort)

    'Testing our proxy
    If Await TelegramBotWebhook.TestWebProxy(MyProxy) Then    

        'Starting Bot with our proxy
        Bot = New TelegramBotWebhook(MyTelegramToken, MyProxyAddress, MyProxyPort, MyNgrokToken)
        
    Else

        'Starting Bot without proxy
        Bot = New TelegramBotWebhook(MyTelegramToken, , MyNgrokToken)
        
    End If

End If

'Setting webhook over ngrok
Bot.SetNgrokWebhook()

...

'One of events. This sends a text message back to the client
Private Sub Bot_OnMessage(sender As Object, e As Message) Handles Bot.OnMessage
    Bot.SendTextMessageAsync(e.Chat.Id, e.Text)
End Sub
```


## Feedback

Feel free to send [feature request or issue](../../issues). Feature requests are always welcome.
