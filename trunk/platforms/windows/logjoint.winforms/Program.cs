using LogJoint.UI;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LogJoint
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(WireupDependenciesAndCreateMainForm());
		}

		static Form WireupDependenciesAndCreateMainForm()
		{
			var mainForm = new UI.MainForm();
			Properties.WebContentConfig webContentConfig = new Properties.WebContentConfig();
			ISynchronizationContext modelSynchronizationContext = new WinFormsSynchronizationContext(mainForm);

			var model = ModelFactory.Create(
				new ModelConfig
				{
					WorkspacesUrl = Properties.Settings.Default.WorkspacesUrl,
					TelemetryUrl = Properties.Settings.Default.TelemetryUrl,
					IssuesUrl = Properties.Settings.Default.IssuesUrl,
					AutoUpdateUrl = Properties.Settings.Default.AutoUpdateUrl,
					PluginsUrl = Properties.Settings.Default.PluginsUrl,
					WebContentCacheConfig = webContentConfig,
					LogsDownloaderConfig = webContentConfig,
					TraceListeners = Properties.Settings.Default.TraceListenerConfig != null ?
						new[] { new TraceListener(Properties.Settings.Default.TraceListenerConfig) } :
						null
				},
				modelSynchronizationContext,
				(storageManager) => new UI.LogsPreprocessorCredentialsCache(
					modelSynchronizationContext,
					storageManager.GlobalSettingsEntry,
					mainForm
				),
				(shutdown, webContentCache, traceSourceFactory) => new UI.Presenters.WebViewTools.Presenter(
					new UI.WebViewTools.WebBrowserDownloaderForm(),
					modelSynchronizationContext,
					webContentCache,
					shutdown,
					traceSourceFactory
				),
				new Drawing.Matrix.Factory(),
				RegularExpressions.LJRegexFactory.Instance
			);

			var viewsFactory = new UI.Presenters.ViewsFactory(mainForm, model);

			mainForm.sourcesListView.SourcesListView.Init(viewsFactory);

			var presentation = UI.Presenters.Factory.Create(
				model,
				new ClipboardAccess(model.TelemetryCollector),
				new ShellOpen(),
				new Alerts(),
				new Alerts(),
				new UI.PromptDialog.Presenter(),
				new AboutDialogConfig(),
				new DragDropHandler(
					model.LogSourcesManager,
					model.LogSourcesPreprocessings,
					model.PreprocessingStepsFactory
				),
				new UI.Presenters.StaticSystemThemeDetector(UI.Presenters.ColorThemeMode.Light),
				viewsFactory
			);

			mainForm.bookmarksManagerView.SetViewModel(presentation.ViewModels.BookmarksManager);
			mainForm.bookmarksManagerView.ListView.SetViewModel(presentation.ViewModels.BookmarksList);
			mainForm.sourcesListView.SourcesListView.SetViewModel(presentation.ViewModels.SourcesList);
			mainForm.sourcesListView.SetViewModel(presentation.ViewModels.SourcesManager);
			mainForm.searchPanelView.searchTextBox.InnerTextBox.SetViewModel(presentation.ViewModels.SearchPanel.QuickSearchTextBox);
			mainForm.searchPanelView.SetViewModel(presentation.ViewModels.SearchPanel);
			mainForm.postprocessingView.SetViewModel(presentation.ViewModels.PostprocessingsTab);
			new UI.HistoryDialog(presentation.ViewModels.HistoryDialog);

			var pluginEntryPoint = new Extensibility.Application(
				model.ExpensibilityEntryPoint,
				presentation.ExpensibilityEntryPoint,
				new Extensibility.View(
					mainForm,
					viewsFactory
				)
			);

			model.PluginsManager.LoadPlugins(pluginEntryPoint, Properties.Settings.Default.LocalPlugins, preferTestPluginEntryPoints: false);

			new PluggableProtocolManager(
				model.TraceSourceFactory,
				model.InstancesCounter,
				model.Shutdown,
				model.TelemetryCollector,
				model.FirstStartDetector,
				model.LaunchUrlParser
			);

			Telemetry.WinFormsUnhandledExceptionsReporter.Setup(model.TelemetryCollector);

			AppInitializer.WireUpCommandLineHandler(presentation.MainFormPresenter, model.CommandLineHandler);

			return mainForm;
		}
	}
}