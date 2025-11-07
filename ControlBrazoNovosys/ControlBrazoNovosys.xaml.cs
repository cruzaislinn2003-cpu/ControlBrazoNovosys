using CommunityToolkit.Maui.Storage;
using System.IO.Ports;
using System.Net.Sockets;

namespace ControlBrazoNovosys;

public partial class ControlBrazoNovosys : ContentPage
{
    SerialPort serialPort = new SerialPort();
    TcpClient tcpClient;
    NetworkStream networkStream;

    bool esConexionSerial = false;
    bool esConexionWiFi = false;

    //lista para guardar posiciones de los servos
    List<(int s1, int s2, int s3, int s4, int s5, int s6)> posicionesGuardadas = new List<(int, int, int, int, int, int)>();

    private CancellationTokenSource ctsReproduccion;

    public ControlBrazoNovosys()
    {
        InitializeComponent();
        LlenarListaDePuertos();
        webView.Source = "index.html";
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private void LlenarListaDePuertos()
    {
        try
        {
            string[] puertos = SerialPort.GetPortNames(); // solo para saber los puertos disponibles
            PuertoSerialPicker.ItemsSource = puertos;

            if (puertos.Length > 0)
                PuertoSerialPicker.SelectedIndex = 0; //por defecto nos va a mostrar uno 
        }
        catch (Exception ex)
        {
        }
    }
    private void CheckBoxParaSerial_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (e.Value)
        {
            esConexionSerial = true;
            esConexionWiFi = false;
            CheckBoxParWifi.IsChecked = false;
            PanelDePuertoSerial.IsVisible = true;
            PanelParaIPdeWifi.IsVisible = false;
            BotonesParaConexion.IsVisible = true;

        }
        else
        {
            PanelDePuertoSerial.IsVisible = false;
            if (!CheckBoxParWifi.IsChecked)
                BotonesParaConexion.IsVisible = false;

        }
    }
    private void CheckBoxParWifi_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (e.Value)
        {
            esConexionSerial = false;
            esConexionWiFi = true;
            CheckBoxParaSerial.IsChecked = false;
            PanelParaIPdeWifi.IsVisible = true;
            PanelDePuertoSerial.IsVisible = false;
            BotonesParaConexion.IsVisible = true;

        }
        else
        {
            PanelParaIPdeWifi.IsVisible = false;
            if (!CheckBoxParaSerial.IsChecked)
                BotonesParaConexion.IsVisible = false;

        }
    }

    private async void BtnConectar_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (esConexionSerial)
            {
                if (!serialPort.IsOpen)
                {
                    string puertoSeleccionado = PuertoSerialPicker.SelectedItem.ToString();
                    serialPort.PortName = puertoSeleccionado;
                    serialPort.BaudRate = 9600;
                    serialPort.Open();

                    //serialPort.WriteLine(puertoSeleccionado);
                    serialPort.Write("J");

                }
            }
            else if (esConexionWiFi)
            {
                string ip = IpEntry.Text.Trim();
                int puerto = 8080;

                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(ip, puerto);
                networkStream = tcpClient.GetStream();

                byte[] buffer = new byte[] { (byte)'J' };
                networkStream.Write(buffer, 0, buffer.Length);
            }

            await DisplayAlert("NOVOSYS STEAM", "Conexión establecida correctamente.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"No se pudo conectar: {ex.Message}", "OK");
        }
    }

    private async void BtnDesconectar_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (esConexionSerial && serialPort.IsOpen)
                serialPort.Close();

            if (esConexionWiFi && tcpClient != null)
            {
                networkStream?.Close();
                tcpClient.Close();
            }

            DisplayAlert("NOVOSYS STEAM", "Conexión cerrada correctamente.", "OK");
        }
        catch (Exception ex)
        {
            DisplayAlert("Error", $"No se pudo desconectar: {ex.Message}", "OK");
        }
    }

    //cree un metodo auziliar para enviar comandos y evitar repetir codigo este metodo va a detectar el tipo de conexcion y lo que hara sera enviar un comando y si tiene un valor igual lo enviara
    private void enviarComando(string comando, byte? valor = null)
    {
        try
        {
            if (esConexionSerial && serialPort != null && serialPort.IsOpen)
            {
                serialPort.Write(comando);
                if (valor.HasValue)
                    serialPort.Write(new byte[] { valor.Value }, 0, 1);
            }
            else if (esConexionWiFi && networkStream != null && tcpClient?.Connected == true)
            {
                if (valor.HasValue)
                    networkStream.Write(new byte[] { (byte)comando[0], valor.Value }, 0, 2);
                else
                    networkStream.Write(new byte[] { (byte)comando[0] }, 0, 1);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al enviar comando '{comando}': {ex.Message}");
        }
    }

    private void servoSlider_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        int valor = (int)e.NewValue;
        lblAngulo.Text = $"{valor}°";
        enviarComando("A", (byte)valor);
    }

    private void servoSlider2_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        int valor = (int)e.NewValue;
        lblAngulo2.Text = $"{valor}°";
        enviarComando("B", (byte)valor);
        webView.Eval($"setBaseGarraRotacionX({valor});");
    }

    private void servoSlider3_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        int valor = (int)e.NewValue;
        lblAngulo3.Text = $"{valor}°";
        enviarComando("C", (byte)valor);
        webView.Eval($"setMunecaRotacionY({valor});");
    }

    private void GuardarButton_Clicked(object sender, EventArgs e)
    {
        posicionesGuardadas.Add((
        (int)servoSlider.Value,
        (int)servoSlider2.Value,
        (int)servoSlider3.Value,
        (int)servoSlider4.Value,
        (int)servoSlider5.Value,
        (int)servoSlider6.Value
        ));
        ActualizarPosicionesEditor();
        enviarComando("G");
        lbl_Etiqueta.Text = "P O S I C I O N    D E L   B R A Z O    G U A R D A D A";
    }

    private async void ExportarButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            var contenido = string.Join(Environment.NewLine,
                posicionesGuardadas.Select(p => $"{p.s1},{p.s2},{p.s3},{p.s4},{p.s5},{p.s6}")
            );

            var bytes = System.Text.Encoding.UTF8.GetBytes(contenido);
            using var stream = new MemoryStream(bytes);

            var resultado = await FileSaver.Default.SaveAsync("posiciones.txt", stream, CancellationToken.None);

            if (resultado.IsSuccessful)
                lbl_Etiqueta.Text = "P O S I C I O N E S    D E L   B R A Z O    E X P O R T A D A S";
                enviarComando("M");
        }
        catch (Exception ex)
        {
            lbl_Etiqueta.Text = "E R R O R   A L   E X P O R T A R   L A S   P O S I C I O N E S    D E L   B R A Z O";
        }
    }

    private async void ReproducirButton_Clicked(object sender, EventArgs e)
    {
        if (posicionesGuardadas.Count < 2)
        {
            lbl_Etiqueta.Text = "N E C E S I T A S   A L   M E N O S   D O S   P O S I C I O N E S";
            return;
        }

        string opcion = await DisplayActionSheet(
            "¿Cuántas veces quieres reproducir?",
            "Cancelar",
            null,
            "1 vez",
            "3 veces",
            "5 veces",
            "Infinito"
        );

        if (opcion == "Cancelar" || opcion == null)
            return;

        int repeticiones = 1;
        bool infinito = false;

        switch (opcion)
        {
            case "1 vez":
                repeticiones = 1;
                break;
            case "3 veces":
                repeticiones = 3;
                break;
            case "5 veces":
                repeticiones = 5;
                break;
            case "Infinito":
                infinito = true;
                break;
        }

        
        ctsReproduccion?.Cancel();
        ctsReproduccion = new CancellationTokenSource();
        var token = ctsReproduccion.Token;

        const int steps = 50;
        const int delay = 20;

        try
        {
            lbl_Etiqueta.Text = "R E P R O D U C I E N D O   P O S I C I O N E S";
            enviarComando("H");

            int indice = 0;
            int contadorRepeticiones = 0;

            while (!token.IsCancellationRequested && (infinito || contadorRepeticiones < repeticiones))
            {
                var a = posicionesGuardadas[indice];
                var b = posicionesGuardadas[(indice + 1) % posicionesGuardadas.Count];

                for (int step = 0; step <= steps; step++)
                {
                    if (token.IsCancellationRequested)
                        return;

                    int val1 = (int)Lerp(a.s1, b.s1, step / (double)steps);
                    int val2 = (int)Lerp(a.s2, b.s2, step / (double)steps);
                    int val3 = (int)Lerp(a.s3, b.s3, step / (double)steps);
                    int val4 = (int)Lerp(a.s4, b.s4, step / (double)steps);
                    int val5 = (int)Lerp(a.s5, b.s5, step / (double)steps);
                    int val6 = (int)Lerp(a.s6, b.s6, step / (double)steps);

                    enviarComando("A", (byte)val1);
                    enviarComando("B", (byte)val2);
                    enviarComando("C", (byte)val3);
                    enviarComando("D", (byte)val4);
                    enviarComando("E", (byte)val5);
                    enviarComando("F", (byte)val6);

                    Dispatcher.Dispatch(() =>
                    {
                        servoSlider.Value = val1;
                        servoSlider2.Value = val2;
                        servoSlider3.Value = val3;
                        servoSlider4.Value = val4;
                        servoSlider5.Value = val5;
                        servoSlider6.Value = val6;
                    });

                    await Task.Delay(delay);
                }

                indice = (indice + 1) % posicionesGuardadas.Count;

                if (indice == 0 && !infinito)
                    contadorRepeticiones++;
            }

            lbl_Etiqueta.Text = infinito
                ? "R E P R O D U C C I Ó N   I N F I N I T A"
                : "R E P R O D U C C I Ó N   F I N A L I Z A D A";
        }
        catch (OperationCanceledException)
        {
            lbl_Etiqueta.Text = "R E P R O D U C C I Ó N   D E T E N I D A";
        }
    }

    private void servoSlider4_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        int valor = (int)e.NewValue;
        lblAngulo4.Text = $"{valor}°";
        enviarComando("D", (byte)valor);
        webView.Eval($"setBrazoRotacionX({valor});");
    }

    private void servoSlider5_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        int valor = (int)e.NewValue;
        lblAngulo5.Text = $"{valor}°";
        enviarComando("E", (byte)valor);
        webView.Eval($"setAntebrazoRotacionX({valor});");
    }

    private void servoSlider6_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        int valor = (int)e.NewValue;
        lblAngulo6.Text = $"{valor}°";
        enviarComando("F", (byte)valor);
        webView.Eval($"setEjeCentralRotacionY({valor});");
    }

    private void DetenerButton_Clicked(object sender, EventArgs e)
    {
        enviarComando("V");
        ctsReproduccion?.Cancel();
        lbl_Etiqueta.Text = "R E P R O D U C C I O N    D E T E N I D A ";
    }

    private async void ImportarButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            var customTxtType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.WinUI, new[] { ".txt" } },
            { DevicePlatform.Android, new[] { "text/plain" } },
            { DevicePlatform.iOS, new[] { "public.plain-text" } },
            { DevicePlatform.MacCatalyst, new[] { "txt" } }
        });

            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Selecciona archivo de posiciones",
                FileTypes = customTxtType
            });

            if (result != null)
            {
                var lines = await File.ReadAllLinesAsync(result.FullPath);
                posicionesGuardadas.Clear();

                foreach (var line in lines)
                {
                    var parts = line.Split(',');
                    if (parts.Length == 6 &&
                        int.TryParse(parts[0], out int s1) &&
                        int.TryParse(parts[1], out int s2) &&
                        int.TryParse(parts[2], out int s3) &&
                        int.TryParse(parts[3], out int s4) &&
                        int.TryParse(parts[4], out int s5) &&
                        int.TryParse(parts[5], out int s6))
                    {
                        posicionesGuardadas.Add((s1, s2, s3, s4, s5, s6));
                        ActualizarPosicionesEditor();
                    }
                }

                if (posicionesGuardadas.Count > 0)
                {
                    var first = posicionesGuardadas[0];
                    servoSlider.Value = first.s1;
                    servoSlider2.Value = first.s2;
                    servoSlider3.Value = first.s3;
                    servoSlider4.Value = first.s4;
                    servoSlider5.Value = first.s5;
                    servoSlider6.Value = first.s6;

                    lblAngulo.Text = $"{first.s1}°";
                    lblAngulo2.Text = $"{first.s2}°";
                    lblAngulo3.Text = $"{first.s3}°";
                    lblAngulo4.Text = $"{first.s4}°";
                    lblAngulo5.Text = $"{first.s5}°";
                    lblAngulo6.Text = $"{first.s6}°";
                }
                lbl_Etiqueta.Text = "P O S I C I O N E S    D E L   B R A Z O    I M P O R T A D A S";
                enviarComando("N");
            }
        }
        catch (Exception ex)
        {
            lbl_Etiqueta.Text = "E R R O R   A L   I M P O R T A R   L A S   P O S I C I O N E S    D E L   B R A Z O";
        }
    }

    private void LimpiarButton_Clicked(object sender, EventArgs e)
    {
        servoSlider.Value = 90;
        servoSlider2.Value = 90;
        servoSlider3.Value = 90;
        servoSlider4.Value = 90;
        servoSlider5.Value = 90;
        servoSlider6.Value = 90;

        posicionesGuardadas.Clear();
        ActualizarPosicionesEditor();
        enviarComando("K");
        lbl_Etiqueta.Text = "B R A Z O  E N  P O S I C I O N  I N I C I A L ! ! !";
    }

    private async void logo_Clicked(object sender, EventArgs e)
    {
        await Browser.OpenAsync("https://novosys.com.mx/", BrowserLaunchMode.SystemPreferred);
    }

    private bool LeerPosicionesDeServos(string line, out (int s1, int s2, int s3, int s4, int s5, int s6) result)
    {
        result = (0, 0, 0, 0, 0, 0);

        if (string.IsNullOrWhiteSpace(line))
            return false;

        var parts = line.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim()).ToArray();

        if (parts.Length != 6)
            return false;

        int s1, s2, s3, s4, s5, s6;

        if (!int.TryParse(parts[0], out s1) ||
            !int.TryParse(parts[1], out s2) ||
            !int.TryParse(parts[2], out s3) ||
            !int.TryParse(parts[3], out s4) ||
            !int.TryParse(parts[4], out s5) ||
            !int.TryParse(parts[5], out s6))
        {
            return false;
        }

        if (s1 < 0 || s1 > 90 || s2 < 0 || s2 > 180 || s3 < 0 || s3 > 180 ||
            s4 < 0 || s4 > 180 || s5 < 0 || s5 > 180 || s6 < 0 || s6 > 180)
        {
            return false;
        }
        result = (s1, s2, s3, s4, s5, s6);
        return true;
    }

    private void ActualizarPosicionesEditor()
    {
        PosicionesEditor.Text = string.Join(Environment.NewLine,
            posicionesGuardadas.Select(p => $"{p.s1},{p.s2},{p.s3},{p.s4},{p.s5},{p.s6}"));
    }

    private void ActualizarDesdeEditor_Clicked(object sender, EventArgs e)
    {
        var text = PosicionesEditor.Text ?? string.Empty;
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var temp = new List<(int, int, int, int, int, int)>();
        var invalid = new List<string>();
        int ln = 0;

        foreach (var raw in lines)
        {
            ln++;
            if (LeerPosicionesDeServos(raw, out var tuple))
            {
                temp.Add(tuple);
            }
            else
            {
                invalid.Add($"{ln}: {raw}");
            }
        }

        if (temp.Count > 0)
        {
            posicionesGuardadas.Clear();
            posicionesGuardadas.AddRange(temp);
            ActualizarPosicionesEditor();
            lbl_Etiqueta.Text = $"S E   C A R G A R O N   {temp.Count}   P O S I C I O N E S   D E S D E   E L   E D I T O R";

            //await DisplayAlert("Éxito", $"Se cargaron {temp.Count} posiciones desde el editor.", "OK");
        }
        else
        {
            lbl_Etiqueta.Text = "N O   S E   E N C O N T R A R O N   L Í N E A S   V Á L I D A S   P A R A   C A R G A R";

            //await DisplayAlert("NOVOSYS STEAM", "No se encontraron líneas válidas para cargar.", "OK");
        }

        if (invalid.Count > 0)
        {
            lbl_Etiqueta.Text = $"L Í N E A S   I N V Á L I D A S   ( N O   S E   C A R G A R O N ) : \n{string.Join("\n", invalid)}";
        }
    }

    
    private async void EliminarDesdeEditor_Clicked(object sender, EventArgs e)
    {
        if (posicionesGuardadas.Count == 0)
        {
            await DisplayAlert("NOVOSYS STEAM", "No hay posiciones para eliminar.", "OK");
            return;
        }

        string VentanaEliminar = await DisplayPromptAsync(
            "Eliminar posición",
            $"Ingresa el número de la posición a eliminar (1 - {posicionesGuardadas.Count})",
            accept: "Eliminar",
            cancel: "Cancelar",
            placeholder: "Número (ej. 1)",
            maxLength: 3,
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(VentanaEliminar)) return;

        if (int.TryParse(VentanaEliminar, out int index) && index >= 1 && index <= posicionesGuardadas.Count)
        {
            posicionesGuardadas.RemoveAt(index - 1);
            ActualizarPosicionesEditor();
            await DisplayAlert("Eliminado", $"Se eliminó la posición #{index}.", "OK");
        }
        else
        {
            await DisplayAlert("Error", "Número inválido.", "OK");
        }
    }

    private void IniciarBanda_Clicked(object sender, EventArgs e)
    {
        enviarComando("S");
        lbl_Etiqueta.Text = "B A N D A   T R A N S P O R T A D O R A   E N   M O V I M I E N T O!!";
    }
    private void DetenerBanda_Clicked(object sender, EventArgs e)
    {
        enviarComando("W");
        lbl_Etiqueta.Text = "S E   D E T U V O   L A   B A N D A   T R A N S P O R T A D O R A!!";
    }

    private int angulo = 90;
    private void aumentarAngulo_Clicked(object sender, EventArgs e)
    {
        if (angulo < 90) 
            angulo += 5;
        lblAngulo.Text = $"{angulo}°";
        servoSlider.Value = angulo;
    }
    private void disminuirAngulo_Clicked(object sender, EventArgs e)
    {
        if (angulo > 0)
            angulo -= 5;
        lblAngulo.Text = $"{angulo}°";
        servoSlider.Value = angulo;
    }
    private void aumentarAngulo2_Clicked(object sender, EventArgs e)
    {
        if (angulo < 180)
            angulo += 5;
        lblAngulo2.Text = $"{angulo}°";
        servoSlider2.Value = angulo;
    }
    private void disminuirAngulo2_Clicked(object sender, EventArgs e)
    {
        if (angulo > 0)
            angulo -= 5;
        lblAngulo2.Text = $"{angulo}°";
        servoSlider2.Value = angulo;
    }
    private void aumentarAngulo3_Clicked(object sender, EventArgs e)
    {
        if (angulo < 180)
            angulo += 5;
        lblAngulo3.Text = $"{angulo}°";
        servoSlider3.Value = angulo;
    }
    private void disminuirAngulo3_Clicked(object sender, EventArgs e)
    {
        if (angulo > 0)
            angulo -= 5;
        lblAngulo3.Text = $"{angulo}°";
        servoSlider3.Value = angulo;
    }
    private void aumentarAngulo4_Clicked(object sender, EventArgs e)
    {
        if (angulo < 180)
            angulo += 5;
        lblAngulo4.Text = $"{angulo}°";
        servoSlider4.Value = angulo;
    }
    private void disminuirAngulo4_Clicked(object sender, EventArgs e)
    {
        if (angulo > 0)
            angulo -= 5;
        lblAngulo4.Text = $"{angulo}°";
        servoSlider4.Value = angulo;
    }
    private void aumentarAngulo5_Clicked(object sender, EventArgs e)
    {
        if (angulo < 180)
            angulo += 5;
        lblAngulo5.Text = $"{angulo}°";
        servoSlider5.Value = angulo;
    }
    private void disminuirAngulo5_Clicked(object sender, EventArgs e)
    {
        if (angulo > 0)
            angulo -= 5;
        lblAngulo5.Text = $"{angulo}°";
        servoSlider5.Value = angulo;
    }
    private void aumentarAngulo6_Clicked(object sender, EventArgs e)
    {
        if (angulo < 180)
            angulo += 5;
        lblAngulo6.Text = $"{angulo}°";
        servoSlider6.Value = angulo;
    }
    private void disminuirAngulo6_Clicked(object sender, EventArgs e)
    {
        if (angulo > 0)
            angulo -= 5;
        lblAngulo6.Text = $"{angulo}°";
        servoSlider6.Value = angulo;
    }
}
