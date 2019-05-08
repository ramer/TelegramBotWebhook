Imports Telegram.Bot.Types

Module MainModule

    Public WithEvents Bot As TelegramBotWebhook

    Sub Main()
        InitializeBot()

        Console.ReadKey()

        DeinitializeBot()
    End Sub

    Public Async Sub InitializeBot()

        Console.Write("Retrieving proxy ... ")
        Dim proxy = Await TelegramBotWebhook.GetWebProxy({"EN", "DE"}, {"RU"})

        If proxy IsNot Nothing Then
            Console.WriteLine("Success")

            Console.WriteLine("Starting Bot with proxy: {0}", proxy.Address)
            Bot = New TelegramBotWebhook(MyTelegramToken, proxy, MyNgrokToken)

        Else
            Console.WriteLine("Failure")

            Dim MyProxy = New Net.WebProxy(MyProxyAddress, MyProxyPort)
            Console.Write("Testing proxy: {0} ... ", MyProxy.Address)

            If Await TelegramBotWebhook.TestWebProxy(MyProxy) Then
                Console.WriteLine("Success")

                Console.WriteLine("Starting Bot with proxy: {0}", MyProxy.Address)
                Bot = New TelegramBotWebhook(MyTelegramToken, MyProxyAddress, MyProxyPort, MyNgrokToken)
            Else
                Console.WriteLine("Failure")

                Console.WriteLine("Starting Bot without proxy")
                Bot = New TelegramBotWebhook(MyTelegramToken, , MyNgrokToken)
            End If

        End If

        Bot.SetNgrokWebhook()
    End Sub

    Public Sub DeinitializeBot()
        Bot.DeleteNgrokWebhook()
    End Sub

    Private Sub Bot_OnMessage(sender As Object, e As Message) Handles Bot.OnMessage
        Bot.SendTextMessageAsync(e.Chat.Id, e.Text)
    End Sub

End Module
