using Microsoft.Maui;
using Microsoft.Maui.Controls;
namespace ControlBrazoNovosys
{
    public partial class App : Application
    {
        private Window ventana;
        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();
        }

        protected override Window CreateWindow(IActivationState estadoInicio)
        {
            this.ventana = base.CreateWindow(estadoInicio);

            var datosPantalla = DeviceDisplay.Current.MainDisplayInfo;

            double ancho = datosPantalla.Width / datosPantalla.Density;
            double alto = datosPantalla.Height / datosPantalla.Density;

            //Se ajusta toda la ventana para ocupar la pantalla completa
            ventana.Width = ancho;
            ventana.Height = alto;
            ventana.X = 0;
            ventana.Y = 0;
            ventana.Created += AjustarPantallaCompleta;

            return ventana;
        }

        private void AjustarPantallaCompleta(object origen, EventArgs e)
        {
#if WINDOWS
    var VentanaSis = (origen as Window).Handler.PlatformView as Microsoft.UI.Xaml.Window;
    if (VentanaSis != null)
    {
        var identificador = WinRT.Interop.WindowNative.GetWindowHandle(VentanaSis);
        var idVentana = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(identificador);
        var ventanaApp = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(idVentana);

        if (ventanaApp.Presenter is Microsoft.UI.Windowing.OverlappedPresenter vista)
        {
            vista.SetBorderAndTitleBar(false, false);
            vista.IsMaximizable = false;
            vista.IsMinimizable = false;
            vista.IsResizable = false;
        }

        //pantalla completa sin barra de tareas
        ventanaApp.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
    }
#endif
        }
    }
}
