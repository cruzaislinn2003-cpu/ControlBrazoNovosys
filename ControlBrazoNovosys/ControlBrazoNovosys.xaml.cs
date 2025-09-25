using CommunityToolkit.Maui.Storage;
using System.IO.Ports;

namespace ControlBrazoNovosys;

public partial class ControlBrazoNovosys : ContentPage
{
    SerialPort serialPort = new SerialPort();

    //lista para guardar posiciones de los servos
    List<(int s1, int s2, int s3, int s4, int s5, int s6)> posicionesGuardadas = new List<(int, int, int, int, int, int)>();

    private CancellationTokenSource ctsReproduccion;

    public ControlBrazoNovosys()
    {
        InitializeComponent();

#if WINDOWS
        var ports = SerialPort.GetPortNames();
        ConexionPicker.ItemsSource = ports.ToList();
#endif
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private async void BtnConectar_Clicked(object sender, EventArgs e)
    {
#if WINDOWS
        try
        {
            if (ConexionPicker.SelectedItem == null)
            {
                await DisplayAlert("Error", "Selecciona un puerto", "OK");
                return;
            }

            if (serialPort.IsOpen)
                serialPort.Close();

            serialPort.PortName = ConexionPicker.SelectedItem.ToString();
            serialPort.BaudRate = 9600;
            serialPort.Open();

            

            serialPort.Write("J"); //azul al conectar
            await DisplayAlert("Éxito", "Conectado por Serial", "OK");
            

        }
        catch (Exception ex)
        {
        
            await DisplayAlert("Error", "No se pudo conectar: " + ex.Message, "OK");
        }
#else
        await DisplayAlert("NOVOSYS STEAM", "Conexión serial solo disponible en Windows", "OK");
#endif
    }

    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            string data = serialPort.ReadLine();
            string[] valores = data.Split(',');

            if (valores.Length == 2)
            {
                string temp = valores[0];
                string hum = valores[1];

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    lbltemperatura.Text = $"Temperatura: {temp} °C | Humedad: {hum} % ";

                });
            }
        }
        catch
        {
        }
    }



    private void servoSlider_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            byte angle = (byte)e.NewValue; try
            {
                
                serialPort.Write("A");
                serialPort.Write(new byte[] { angle }, 0, 1); //envia el angulo
                lblAngulo.Text = $"{angle}°";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al enviar datos: " + ex.Message);
            }
        }
    }

    private void servoSlider2_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            byte angle = (byte)e.NewValue; try
            {
                serialPort.Write("B");
                serialPort.Write(new byte[] { angle }, 0, 1); //envia el angulo
                lblAngulo2.Text = $"{angle}°";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al enviar datos: " + ex.Message);
            }
        }
    }

    private void servoSlider3_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            byte angle = (byte)e.NewValue; try
            {
                serialPort.Write("C");
                serialPort.Write(new byte[] { angle }, 0, 1); //envia el angulo
                lblAngulo3.Text = $"{angle}°";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al enviar datos: " + ex.Message);
            }
        }
    }

    private async void GuardarButton_Clicked(object sender, EventArgs e)
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

        if (serialPort?.IsOpen == true)
            serialPort.Write("G"); //amarillo al guardar
        await DisplayAlert("NOVOSYS STEAM", "POSICIÓN GUARDADA", "OK");
        
        lbl_Etiqueta.Text = "P O S I C I O N    D E L   B R A Z O    G U A R D A D A";
    }

    private async void ExportarButton_Clicked(object sender, EventArgs e)
    {
        if (serialPort?.IsOpen == true)
            serialPort.Write("M");
        try
        {
            var contenido = string.Join(Environment.NewLine,
                posicionesGuardadas.Select(p => $"{p.s1},{p.s2},{p.s3},{p.s4},{p.s5},{p.s6}")
            );

            var bytes = System.Text.Encoding.UTF8.GetBytes(contenido);
            using var stream = new MemoryStream(bytes);

            var resultado = await FileSaver.Default.SaveAsync("posiciones.txt", stream, CancellationToken.None);

            if (resultado.IsSuccessful)
                await DisplayAlert("NOVOSYS STEAM", $"Archivo exportado:\n{resultado.FilePath}", "OK");
            
            lbl_Etiqueta.Text = "P O S I C I O N E S    D E L   B R A Z O    E X P O R T A D A S";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void ReproducirButton_Clicked(object sender, EventArgs e)
    {
        if (serialPort?.IsOpen == true)
            serialPort.Write("V");
        lbl_Etiqueta.Text = "R E P R O D U C I E N D O   P O S I C I O N E S";

        if (posicionesGuardadas.Count < 2)
        {
            await DisplayAlert("Info", "Necesitas al menos 2 posiciones guardadas.", "OK");
            return;
        }
        
        
        ctsReproduccion?.Cancel();
        ctsReproduccion = new CancellationTokenSource();
        var token = ctsReproduccion.Token;

        const int steps = 50;
        const int delay = 20; 

        try
        {
            int indice = 0;
            while (!token.IsCancellationRequested) 
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

                    if (serialPort?.IsOpen == true)
                    {
                        try
                        {
                            serialPort.Write("A"); serialPort.Write(new byte[] { (byte)val1 }, 0, 1);
                            serialPort.Write("B"); serialPort.Write(new byte[] { (byte)val2 }, 0, 1);
                            serialPort.Write("C"); serialPort.Write(new byte[] { (byte)val3 }, 0, 1);
                            serialPort.Write("D"); serialPort.Write(new byte[] { (byte)val4 }, 0, 1);
                            serialPort.Write("E"); serialPort.Write(new byte[] { (byte)val5 }, 0, 1);
                            serialPort.Write("F"); serialPort.Write(new byte[] { (byte)val6 }, 0, 1);
                        }
                        catch (Exception ex)
                        {
                            
                            Console.WriteLine("Error al enviar datos: " + ex.Message);
                        }
                    }

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
            }
        }
        catch (OperationCanceledException)
        {
            await DisplayAlert("NOVOSYS STEAM", "¡Reproducción detenida!", "OK");
        }
    }

    private void servoSlider4_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            byte angle = (byte)e.NewValue; try
            {
                serialPort.Write("D");
                serialPort.Write(new byte[] { angle }, 0, 1); //envia el angulo
                lblAngulo4.Text = $"{angle}°";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al enviar datos: " + ex.Message);
            }
        }
    }

    private void servoSlider5_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            byte angle = (byte)e.NewValue; try
            {
                serialPort.Write("E");
                serialPort.Write(new byte[] { angle }, 0, 1); //envia el angulo
                lblAngulo5.Text = $"{angle}°";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al enviar datos: " + ex.Message);
            }
        }
    }

    private void servoSlider6_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            byte angle = (byte)e.NewValue; try
            {
                serialPort.Write("F");
                serialPort.Write(new byte[] { angle }, 0, 1); //envia el angulo
                lblAngulo6.Text = $"{angle}°";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al enviar datos: " + ex.Message);
            }
        }
    }

    private async void DetenerButton_Clicked(object sender, EventArgs e)
    {
        if (serialPort?.IsOpen == true)
            serialPort.Write("H"); //rojo al detener
        ctsReproduccion?.Cancel();
        await DisplayAlert("NOVOSYS STEAM", "Movimiento detenido", "OK");

        lbl_Etiqueta.Text = "R E P R O D U C C I O N    D E T E N I D A ";
    }

    private async void ImportarButton_Clicked(object sender, EventArgs e)
    {
        if (serialPort?.IsOpen == true)
            serialPort.Write("N");
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

                await DisplayAlert("NOVOSYS STEAM", "Archivo importado correctamente", "OK");
                lbl_Etiqueta.Text = "P O S I C I O N E S    D E L   B R A Z O    I M P O R T A D A S";
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void LimpiarButton_Clicked(object sender, EventArgs e)
    {
        servoSlider.Value = 90;
        servoSlider2.Value = 90;
        servoSlider3.Value = 90;
        servoSlider4.Value = 90;
        servoSlider5.Value = 90;
        servoSlider6.Value = 90;

        posicionesGuardadas.Clear();
        ActualizarPosicionesEditor();
        if (serialPort?.IsOpen == true)
            serialPort.Write("K");

        await DisplayAlert("NOVOSYS STEAM", "Posiciones reseteadas", "OK");
        lbl_Etiqueta.Text = "B R A Z O  E N  P O S I C I O N  I N I C I A L ! ! !";
    }

    private async void logo_Clicked(object sender, EventArgs e)
    {
        await Browser.OpenAsync("https://novosys.com.mx/", BrowserLaunchMode.SystemPreferred);
    }

    private bool TryParsePositionLine(string line, out (int s1, int s2, int s3, int s4, int s5, int s6) result)
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


    private async void ActualizarDesdeEditor_Clicked(object sender, EventArgs e)
    {
        var text = PosicionesEditor.Text ?? string.Empty;
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var temp = new List<(int, int, int, int, int, int)>();
        var invalid = new List<string>();
        int ln = 0;

        foreach (var raw in lines)
        {
            ln++;
            if (TryParsePositionLine(raw, out var tuple))
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
            await DisplayAlert("Éxito", $"Se cargaron {temp.Count} posiciones desde el editor.", "OK");
        }
        else
        {
            await DisplayAlert("NOVOSYS STEAM", "No se encontraron líneas válidas para cargar.", "OK");
        }

        if (invalid.Count > 0)
        {
            await DisplayAlert("Aviso", $"Líneas inválidas (no se cargaron):\n{string.Join("\n", invalid)}", "OK");
        }
    }

    
    private async void EliminarDesdeEditor_Clicked(object sender, EventArgs e)
    {
        if (posicionesGuardadas.Count == 0)
        {
            await DisplayAlert("NOVOSYS STEAM", "No hay posiciones para eliminar.", "OK");
            return;
        }

        string prompt = await DisplayPromptAsync(
            "Eliminar posición",
            $"Ingresa el número de la posición a eliminar (1 - {posicionesGuardadas.Count})",
            accept: "Eliminar",
            cancel: "Cancelar",
            placeholder: "Número (ej. 1)",
            maxLength: 3,
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(prompt)) return;

        if (int.TryParse(prompt, out int index) && index >= 1 && index <= posicionesGuardadas.Count)
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

    private async void IniciarBanda_Clicked(object sender, EventArgs e)
    {
        if (serialPort?.IsOpen == true)
            serialPort.Write("S");
        await DisplayAlert("NOVOSYS STEAM", "¡Banda transportadora en movimiento!", "OK");
    }

    private async void DetenerBanda_Clicked(object sender, EventArgs e)
    {
        if (serialPort?.IsOpen == true)
            serialPort.Write("W");
        await DisplayAlert("NOVOSYS STEAM", "¡Banda transportadora detenida!", "OK");
    }
}