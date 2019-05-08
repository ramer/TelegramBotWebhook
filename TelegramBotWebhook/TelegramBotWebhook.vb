Imports System.Collections.Specialized
Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports Newtonsoft.Json
Imports Telegram.Bot.Args
Imports Telegram.Bot.Types
Imports Telegram.Bot.Types.Enums

Public Class TelegramBotWebhook
    Inherits Telegram.Bot.TelegramBotClient

    Const webhookport As Integer = 51595

    Public ReadOnly Property TelegramToken As String
    Public ReadOnly Property NgrokToken As String

    Private usengrok As Boolean

    Private cts As CancellationTokenSource
    Private ct As CancellationToken

    Private ngroktask As Task
    Private webhooktask As Task

    Public Shadows Event OnUpdate(sender As Object, e As Update)
    Public Shadows Event OnMessage(sender As Object, e As Message)
    Public Shadows Event OnInlineQuery(sender As Object, e As InlineQuery)
    Public Shadows Event OnInlineResultChosen(sender As Object, e As ChosenInlineResult)
    Public Shadows Event OnCallbackQuery(sender As Object, e As CallbackQuery)
    Public Shadows Event OnMessageEdited(sender As Object, e As Message)

    Sub New(token As String, WebProxy As IWebProxy, Optional ngrokToken As String = Nothing)
        MyBase.New(token, WebProxy)

        Me.TelegramToken = token
        Me.NgrokToken = ngrokToken

        usengrok = Not String.IsNullOrEmpty(ngrokToken)
    End Sub

    Sub New(telegramToken As String, Optional httpClient As HttpClient = Nothing, Optional ngrokToken As String = Nothing)
        MyBase.New(telegramToken, httpClient)

        Me.TelegramToken = telegramToken
        Me.NgrokToken = ngrokToken

        usengrok = Not String.IsNullOrEmpty(ngrokToken)
    End Sub

    Sub New(telegramToken As String, proxyAddress As String, proxyPort As Integer, Optional ngrokToken As String = Nothing)
        MyBase.New(telegramToken, New WebProxy(proxyAddress, proxyPort))

        Me.TelegramToken = telegramToken
        Me.NgrokToken = ngrokToken

        usengrok = Not String.IsNullOrEmpty(ngrokToken)
    End Sub

    Public Sub SetNgrokWebhook(Optional allowedUpdates As UpdateType() = Nothing)
        cts = New CancellationTokenSource
        ct = cts.Token

        If usengrok Then
            ngroktask = Task.Run(Sub() InitializeNgrok(ct))
            webhooktask = Task.Run(Sub() InitializeWebhook(ct))
        Else
            Throw New ArgumentNullException("ngrokToken")
        End If
    End Sub

    Public Sub DeleteNgrokWebhook()
        If cts IsNot Nothing Then cts.Cancel()

        If usengrok Then
            Task.WaitAll(ngroktask, webhooktask)
        Else
            Throw New ArgumentNullException("ngrokToken")
        End If
    End Sub

    Private Sub InitializeNgrok(ct As CancellationToken)
        Dim oldngrokproc As New List(Of Process)(Process.GetProcessesByName("ngrok"))
        oldngrokproc.ForEach(Sub(p) p.Kill())

        Dim ngrokproc As Process
        Dim psi As New ProcessStartInfo()
        psi.FileName = "ngrok.exe"
        psi.CreateNoWindow = True
        psi.UseShellExecute = False
        psi.RedirectStandardError = True
        psi.RedirectStandardOutput = True
        ngrokproc = New Process
        ngrokproc.StartInfo = psi


        While Not ct.IsCancellationRequested
            Try
                psi.Arguments = String.Format("http {0} -authtoken {1} -host-header=""localhost:{0}"" -log ""stdout""", webhookport, NgrokToken)
                ngrokproc.Start()

                Do
                    Dim outputtask = ngrokproc.StandardOutput.ReadLineAsync

                    Try
                        outputtask.Wait(ct)
                    Catch ex As OperationCanceledException
                        Exit While
                    End Try

                    Dim Line = outputtask.Result
                    If Not String.IsNullOrEmpty(Line) Then
                        Debug.WriteLine(Line)
                        Dim lineargs = ParseArguments(Line) '.Split({" "}, StringSplitOptions.RemoveEmptyEntries)
                        If lineargs.Length > 6 AndAlso lineargs(2) = "msg=""started tunnel""" AndAlso lineargs(6).StartsWith("url=https") Then MyBase.SetWebhookAsync(lineargs(6).Substring(4))
                    End If
                Loop Until ngrokproc.HasExited Or ct.IsCancellationRequested

                If ngrokproc.ExitCode <> 0 Then
                    Debug.WriteLine("ngrok closed suddenly")
                End If

            Catch ex As Exception
                Debug.WriteLine(ex.Message)
                Exit While
            End Try
        End While

        If Not ngrokproc.HasExited Then ngrokproc.Kill()
    End Sub

    Private Shared Function ParseArguments(ByVal line As String) As String()
        Dim parmChars As Char() = line.ToCharArray()
        Dim inQuote As Boolean = False

        For index As Integer = 0 To parmChars.Length - 1
            If parmChars(index) = """"c Then inQuote = Not inQuote
            If Not inQuote AndAlso parmChars(index) = " "c Then parmChars(index) = vbLf
        Next

        Return (New String(parmChars)).Split(vbLf)
    End Function

    Private Sub InitializeWebhook(ct As CancellationToken)
        Dim listener As New HttpListener
        listener.Prefixes.Add("http://localhost:" & webhookport & "/")

        listener.Start()
        Do
            Dim contexttask = listener.GetContextAsync

            Try
                contexttask.Wait(ct)
            Catch ex As OperationCanceledException
                Exit Do
            End Try

            Dim context = contexttask.Result
            Dim request = context.Request
            Dim response = context.Response

            Using ms As New MemoryStream
                request.InputStream.CopyToAsync(ms)

                Dim bodystring = Text.Encoding.UTF8.GetString(ms.ToArray, 0, ms.Length)
                Dim update As Update = JsonConvert.DeserializeObject(Of Update)(bodystring)

                RaiseEvent OnUpdate(Me, update)

                Select Case update.Type
                    Case UpdateType.Message
                        RaiseEvent OnMessage(Me, update.Message)
                    Case UpdateType.InlineQuery
                        RaiseEvent OnInlineQuery(Me, update.InlineQuery)
                    Case UpdateType.ChosenInlineResult
                        RaiseEvent OnInlineResultChosen(Me, update.ChosenInlineResult)
                    Case UpdateType.CallbackQuery
                        RaiseEvent OnCallbackQuery(Me, update.CallbackQuery)
                    Case UpdateType.EditedMessage
                        RaiseEvent OnMessageEdited(Me, update.Message)
                End Select
            End Using

            response.StatusCode = 200
            response.Close()

        Loop Until ct.IsCancellationRequested
        listener.Stop()
    End Sub

    Private Sub TelegramBotWebhookWrapper_OnUpdate(sender As Object, e As UpdateEventArgs) Handles MyBase.OnUpdate
        RaiseEvent OnUpdate(Me, e.Update)

        Select Case e.Update.Type
            Case UpdateType.Message
                RaiseEvent OnMessage(Me, e.Update.Message)
            Case UpdateType.InlineQuery
                RaiseEvent OnInlineQuery(Me, e.Update.InlineQuery)
            Case UpdateType.ChosenInlineResult
                RaiseEvent OnInlineResultChosen(Me, e.Update.ChosenInlineResult)
            Case UpdateType.CallbackQuery
                RaiseEvent OnCallbackQuery(Me, e.Update.CallbackQuery)
            Case UpdateType.EditedMessage
                RaiseEvent OnMessageEdited(Me, e.Update.Message)
        End Select
    End Sub

    Private Sub TelegramBotWebhookWrapper_OnMessage(sender As Object, e As MessageEventArgs) Handles MyBase.OnMessage
        RaiseEvent OnMessage(Me, e.Message)
    End Sub

    Private Sub TelegramBotWebhookWrapper_OnInlineQuery(sender As Object, e As InlineQueryEventArgs) Handles MyBase.OnInlineQuery
        RaiseEvent OnInlineQuery(Me, e.InlineQuery)
    End Sub

    Private Sub TelegramBotWebhookWrapper_OnInlineResultChosen(sender As Object, e As ChosenInlineResultEventArgs) Handles MyBase.OnInlineResultChosen
        RaiseEvent OnInlineResultChosen(Me, e.ChosenInlineResult)
    End Sub

    Private Sub TelegramBotWebhookWrapper_OnCallbackQuery(sender As Object, e As CallbackQueryEventArgs) Handles MyBase.OnCallbackQuery
        RaiseEvent OnCallbackQuery(Me, e.CallbackQuery)
    End Sub

    Private Sub TelegramBotWebhookWrapper_OnMessageEdited(sender As Object, e As MessageEventArgs) Handles MyBase.OnMessageEdited
        RaiseEvent OnMessageEdited(Me, e.Message)
    End Sub

    Public Shared Async Function GetWebProxy(Optional inludeCountries As String() = Nothing, Optional excludeCountries As String() = Nothing) As Task(Of WebProxy)
        Dim httpclient As New HttpClient
        Dim parameters As NameValueCollection = System.Web.HttpUtility.ParseQueryString(String.Empty)
        For Each c In inludeCountries
            parameters.Add("country", c)
        Next
        For Each c In excludeCountries
            parameters.Add("notCountry", c)
        Next
        parameters.Add("protocol", "http")
        parameters.Add("maxSecondsToFirstByte", "3")

        Dim proxy As ProxyServer
        Do
            Dim response = Await httpclient.GetAsync("https://api.getproxylist.com/proxy?" & parameters.ToString)
            Dim content = Await response.Content.ReadAsStringAsync
            proxy = JsonConvert.DeserializeObject(Of ProxyServer)(content)
            If proxy.ip Is Nothing Or proxy.port = 0 Then Return Nothing
        Loop Until Await TestWebProxy(proxy.GetWebProxy)

        Return proxy.GetWebProxy
    End Function

    Public Shared Async Function TestWebProxy(proxy As WebProxy) As Task(Of Boolean)
        Dim httpclienthandler As New HttpClientHandler
        httpclienthandler.Proxy = proxy
        Dim httpclient As New HttpClient(httpclienthandler)

        Dim proxytask = httpclient.GetAsync("https://api.telegram.org/")
        Dim proxytaskfinished = Await Task.WhenAny(proxytask, Task.Delay(20000)) Is proxytask
        Return proxytaskfinished AndAlso proxytask.IsCompleted AndAlso proxytask.Result.StatusCode = 200
    End Function

End Class

Public Class ProxyServer
    Public Property ip As String
    Public Property port As Integer
    Public Property protocol As String
    Public Property anonymity As String
    Public Property lastTested As String
    Public Property allowsRefererHeader As Boolean
    Public Property allowsUserAgentHeader As Boolean
    Public Property allowsCustomHeaders As Boolean
    Public Property allowsCookies As Boolean
    Public Property allowsPost As Boolean
    Public Property allowsHttps As Boolean
    Public Property country As String
    Public Property connectTime As String
    Public Property downloadSpeed As String
    Public Property secondsToFirstByte As String
    Public Property uptime As String

    Public Function GetWebProxy() As WebProxy
        Return New WebProxy(ip, port)
    End Function

End Class