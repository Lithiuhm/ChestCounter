using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using ChestCounter;

/// <summary>
/// Punto de entrada. Main lleva [STAThread]: Windows exige un hilo STA para el portapapeles y los diálogos
/// (con instrucciones de nivel superior el hilo principal arrancaba en MTA y "Copiar" fallaba en silencio).
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // --standby: el proceso invisible que espera a que arranque Fortnite ("abrir con Fortnite"). Sin consola ni ventana.
        if (args.Contains("--standby"))
        {
            var dataIndex = Array.IndexOf(args, "--data");
            return OperatingSystem.IsWindows()
                ? FortniteWatch.RunStandby(dataIndex >= 0 && dataIndex + 1 < args.Length ? args[dataIndex + 1] : Store.DefaultFolder())
                : 1;
        }

        // Estilo de Windows para los controles: debe fijarse antes de crear la primera ventana (también los mensajes).
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Las opciones de línea de comandos escriben en la terminal que lanzó el exe; si no, la aplicación es una ventana
        // y la consola (que el código de análisis usa para sus mensajes) se oculta.
        var cliMode = args.Any(a => a is "--once" or "--help" or "-h" or "--overlay-preview");
        if (OperatingSystem.IsWindows())
        {
            NativeConsole.Ensure(attachToParent: cliMode);
            if (!cliMode)
            {
                NativeConsole.Hide();
            }
        }
        Console.OutputEncoding = Encoding.UTF8;

        // Un fallo no debe cerrar la aplicación sin dejar rastro (el exe normalmente se abre con doble clic).
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            ErrorLog.Write("Unexpected error", e.ExceptionObject);
            if (cliMode)
            {
                Console.WriteLine(L.T("Error inesperado: ", "Unexpected error: ") + e.ExceptionObject);
            }
            else
            {
                MessageBox.Show(L.T("Error inesperado. Los detalles están en errors.log, en la carpeta de datos.",
                        "Unexpected error. The details are in errors.log, in the data folder.") + "\n\n" + e.ExceptionObject,
                    "ChestCounter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        // ---- Argumentos ----------------------------------------------------------------------------------
        string? dataDir = null, demosArg = null;
        var once = false;
        var reset = false;
        var auto = false;          // abierto por el proceso en espera cuando Fortnite arranca: se cierra solo al cerrar Fortnite
        var afterUpdate = false;   // reiniciado por el actualizador: espera a que la instancia anterior termine
        string? previewPath = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--data": dataDir = args[++i]; break;
                case "--demos": demosArg = args[++i]; break;
                case "--lang": i++; break; // compatibilidad: el idioma lo elige Windows
                case "--once": once = true; break;
                case "--reset-config": reset = true; break;
                case "--overlay-preview": previewPath = args[++i]; break;
                case "--auto": auto = true; break;
                case "--after-update": afterUpdate = true; break;
                case "-h":
                case "--help":
                    Console.WriteLine(L.Big("help", """
                        Contador de cofres de Fortnite

                        Opciones:
                          --demos <carpeta>   carpeta de los replays (por defecto %LOCALAPPDATA%\FortniteGame\Saved\Demos)
                          --data <carpeta>    carpeta de los datos (por defecto %APPDATA%\ChestCounter)
                          --once              analiza los replays terminados, muestra el resumen y sale (sin ventana)
                          --reset-config      vuelve a mostrar las preguntas iniciales
                          --overlay-preview <archivo.png>   guarda vistas previas del overlay y sale
                        """, """
                        Fortnite Chest Counter

                        Options:
                          --demos <folder>    replays folder (default %LOCALAPPDATA%\FortniteGame\Saved\Demos)
                          --data <folder>     data folder (default %APPDATA%\ChestCounter)
                          --once              analyze the finished replays, show the summary and quit (no window)
                          --reset-config      show the first-run questions again
                          --overlay-preview <file.png>   save previews of the overlay and quit
                        """));
                    return 0;
            }
        }

        dataDir ??= Store.DefaultFolder();
        var store = new Store(dataDir);
        ErrorLog.FilePath = store.ErrorLogPath;

        // Una sola instancia por carpeta de datos: dos contarían las mismas partidas.
        using var instance = new Mutex(false, AppInfo.InstanceMutexFor(dataDir));
        if (previewPath == null && !AcquireInstance(instance, afterUpdate ? TimeSpan.FromSeconds(20) : TimeSpan.Zero))
        {
            if (auto)
            {
                return 0; // abierto con Fortnite cuando ya estaba abierto: nada que hacer
            }
            var alreadyOpen = L.T("El contador de cofres ya está abierto: mira en la barra de tareas.",
                "The chest counter is already open: look in the taskbar.");
            if (cliMode)
            {
                Console.WriteLine("  " + alreadyOpen);
            }
            else
            {
                MessageBox.Show(alreadyOpen, "ChestCounter", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return 0;
        }

        // ---- Configuración -------------------------------------------------------------------------------
        var config = reset ? null : store.LoadConfig();
        var firstRun = config == null;
        config ??= new Config();
        config.Language = L.Code;

        if (demosArg != null) config.DemosFolder = demosArg;
        var defaultDemos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteGame", "Saved", "Demos");
        if (string.IsNullOrWhiteSpace(config.DemosFolder))
        {
            config.DemosFolder = defaultDemos;
        }

        if (previewPath != null)
        {
            // Desarrollo: dibuja el overlay fuera de pantalla y guarda PNG (en espera, partida en curso y en vivo), luego sale.
            store.LoadPrizeNames();
            var previewData = Profiles.View(store.LoadData(), config);
            // Solo en memoria: las 3 últimas partidas cuentan como "esta sesión", para una línea de sesión realista.
            foreach (var m in previewData.Matches.OrderByDescending(m => m.MatchStartUtc ?? m.AnalyzedAtUtc).Take(3))
            {
                m.MatchStartUtc = DateTime.UtcNow;
            }
            if (!OperatingSystem.IsWindows())
            {
                return 1;
            }
            NativeOverlay.Start(-20000, -20000, false, (_, _) => { }, _ => { });
            var fakeLive = new MatchRecord { ChestsOpened = 12, TreasureFinds = { new TreasureFind { Item = "Treasure" } } };
            foreach (var (suffix, live, inProgress) in new[] { ("-espera", (MatchRecord?)null, false), ("-en-curso", null, true), ("-en-vivo", fakeLive, true) })
            {
                NativeOverlay.Update(OverlayModel.Build(previewData, live, inProgress, Profiles.InitialChests(config, previewData.Matches.Select(Profiles.Key).FirstOrDefault())));
                Thread.Sleep(600);
                var file = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(previewPath)) ?? ".", Path.GetFileNameWithoutExtension(previewPath) + suffix + ".png");
                Console.WriteLine(NativeOverlay.Snapshot(file)
                    ? L.F("Vista previa: {0}", "Preview: {0}", file)
                    : L.F("Error al capturar: {0}", "Capture failed: {0}", file));
            }
            return 0;
        }

        LowerPriority();
        store.LoadPrizeNames();
        var data = store.LoadData();
        var tracker = new Tracker(store, config, data);
        // Antes de cualquier escaneo: si no, los replays se analizarían dos veces.
        var reanalyzing = tracker.UpgradeIfNeeded();

        // ---- Modo de línea de comandos -------------------------------------------------------------------
        if (once)
        {
            tracker.Scan(ignoreQuietPeriod: true);
            ReportWriter.WriteAll(store, config, data);
            tracker.PrintSummary();
            Console.WriteLine(L.F("  Informe: {0}", "  Report: {0}", store.ReportPath));
            return 0;
        }

        // ---- Primer uso: aviso de las repeticiones y, si hay replays, si contarlos -------------------------------------------
        if (firstRun)
        {
            MessageBox.Show(
                L.Big("intro", "Este contador cuenta tus cofres a partir de los replays de Fortnite.\n\n" +
                    "Importante: en Fortnite activa Ajustes → Juego → Replays → Grabar replays.\n" +
                    "Sin eso, Fortnite no guarda replays y no hay nada que contar.",
                    "This counter counts your chests from the Fortnite replays.\n\n" +
                    "Important: in Fortnite turn on Settings → Game → Replays → Record Replays.\n" +
                    "Without it, Fortnite saves no replays and there is nothing to count."),
                "ChestCounter", MessageBoxButtons.OK, MessageBoxIcon.Information);

            var existing = tracker.UnprocessedFiles().Where(f => ReplayHeaderProbe.IsLive(f.FullName) != true).ToList();
            if (existing.Count > 0)
            {
                var count = L.Count(existing.Count, "replay", "replays", "replay", "replays");
                var answer = MessageBox.Show(
                    L.F("Hay {0} en tu PC. ¿Contarlos también?\n\nSí: se analizan ahora.   No: solo cuentan tus próximas partidas.",
                        "There are {0} on your PC. Count them too?\n\nYes: they are analyzed now.   No: only your next matches count.", count),
                    "ChestCounter", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.No)
                {
                    tracker.MarkSkipped(existing);
                }
            }
            // "Abrir con Fortnite" queda desactivado: se activa en Ajustes.
        }
        store.SaveConfig(config);

        if (OperatingSystem.IsWindows())
        {
            Updater.CleanupOld();
            if (config.StartWithFortnite)
            {
                FortniteWatch.Apply(true, dataDir); // sigue la ruta del exe si se movió o se actualizó
            }
            if (Process.GetProcessesByName("ChestTracker") is { Length: > 0 } oldTool)
            {
                MessageBox.Show(
                    L.T("La versión antigua (ChestTracker.exe) sigue abierta: ciérrala, o las dos contarían a la vez.",
                        "The old version (ChestTracker.exe) is still open: close it, otherwise both count at the same time."),
                    "ChestCounter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                foreach (var p in oldTool) p.Dispose();
            }
        }

        // ---- Ventana -------------------------------------------------------------------------------------
        var monitor = new ChestCounter.Monitor(store, config, data, tracker, dataDir) { AutoMode = auto };
        if (reanalyzing)
        {
            monitor.SetNotice(L.T("Nueva versión del análisis: se vuelven a leer los replays (sin duplicados).",
                "New analysis version: the replays are read again (no duplicates)."));
        }

        var form = new MainForm(monitor, store);
        monitor.CloseRequested += () =>
        {
            try { form.BeginInvoke(new Action(form.Close)); }
            catch (InvalidOperationException) { /* la ventana aún no existe */ }
        };

        monitor.Start();
        Application.Run(form);
        monitor.Stop();

        // Nueva versión instalada: arranca el exe nuevo.
        if (monitor.RestartAfterUpdate && AppInfo.ExePath is { } newExe)
        {
            try { instance.ReleaseMutex(); } catch { }
            var restartArgs = string.Join(" ", args.Where(a => a != "--after-update").Select(a => a.Contains(' ') ? $"\"{a}\"" : a).Append("--after-update"));
            Process.Start(new ProcessStartInfo(newExe, restartArgs) { UseShellExecute = true })?.Dispose();
        }
        return 0;

        // ---- Ayudantes -----------------------------------------------------------------------------------

        static bool AcquireInstance(Mutex m, TimeSpan wait)
        {
            try
            {
                return m.WaitOne(wait);
            }
            catch (AbandonedMutexException)
            {
                return true; // la instancia anterior se cerró mal: ahora es nuestra
            }
        }

        // Prioridad baja + modo eficiencia de Windows 11 (EcoQoS): el juego siempre va primero.
        static void LowerPriority()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            try { EcoQos.Enable(); } catch { }
        }
    }
}
