using LogJoint.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
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
			var tracer = CreateTracer();

			using (tracer.NewFrame)
			{
				ILogProviderFactoryRegistry logProviderFactoryRegistry = new LogProviderFactoryRegistry();
				IFormatDefinitionsRepository formatDefinitionsRepository = new DirectoryFormatsRepository(null);
				IUserDefinedFormatsManager userDefinedFormatsManager = new UserDefinedFormatsManager(formatDefinitionsRepository, logProviderFactoryRegistry);
				var appInitializer = new AppInitializer(tracer, userDefinedFormatsManager, logProviderFactoryRegistry);
				tracer.Info("app initializer created");
				var mainForm = new UI.MainForm();
				tracer.Info("main form created");
				IInvokeSynchronization invokingSynchronization = new InvokeSynchronization(mainForm);
				TempFilesManager tempFilesManager = LogJoint.TempFilesManager.GetInstance();
				UI.HeartBeatTimer heartBeatTimer = new UI.HeartBeatTimer(mainForm);
				UI.Presenters.IViewUpdates viewUpdates = heartBeatTimer;
				var modelHost = new UI.ModelHost(tracer);
				IFiltersFactory filtersFactory = new FiltersFactory();
				IBookmarksFactory bookmarksFactory = new BookmarksFactory();
				var bookmarks = bookmarksFactory.CreateBookmarks();
				var desktopStorageImplementation = new Persistence.DesktopStorageImplementation();
				Persistence.IStorageManager storageManager = new Persistence.StorageManager(
					new Persistence.RealEnvironment(),
					desktopStorageImplementation
				);
				Persistence.IFirstStartDetector firstStartDetector = desktopStorageImplementation;
				IShutdown shutdown = new AppShutdown();
				MultiInstance.IInstancesCounter instancesCounter = new MultiInstance.InstancesCounter(shutdown);
				MRU.IRecentlyUsedEntities recentlyUsedLogs = new MRU.RecentlyUsedEntities(storageManager, logProviderFactoryRegistry);
				IFormatAutodetect formatAutodetect = new FormatAutodetect(recentlyUsedLogs, logProviderFactoryRegistry);
				Progress.IProgressAggregator progressAggregator = new Progress.ProgressAggregator(heartBeatTimer, invokingSynchronization);

				IAdjustingColorsGenerator colorGenerator = new AdjustingColorsGenerator(
					new PastelColorsGenerator(),
					storageManager.GlobalSettingsAccessor.Appearance.ColoringBrightness
				);

				IModelThreads modelThreads = new ModelThreads(colorGenerator);

				ILogSourcesManager logSourcesManager = new LogSourcesManager(
					modelHost,
					heartBeatTimer,
					invokingSynchronization,
					modelThreads,
					tempFilesManager,
					storageManager,
					bookmarks,
					storageManager.GlobalSettingsAccessor
				);

				Workspaces.IWorkspacesManager workspacesManager = new Workspaces.WorkspacesManager(
					logSourcesManager,
					logProviderFactoryRegistry,
					storageManager,
					new Workspaces.Backend.AzureWorkspacesBackend(),
					tempFilesManager,
					recentlyUsedLogs
				);

				Telemetry.ITelemetryCollector telemetryCollector = new Telemetry.TelemetryCollector(
					storageManager,
					new Telemetry.AzureTelemetryUploader(),
					invokingSynchronization,
					instancesCounter,
					shutdown,
					logSourcesManager
				);
				tracer.Info("telemetry created");

				AppLaunch.IAppLaunch pluggableProtocolManager = new PluggableProtocolManager(
					instancesCounter, 
					shutdown, 
					telemetryCollector,
					firstStartDetector
				);

				Preprocessing.IPreprocessingManagerExtensionsRegistry preprocessingManagerExtensionsRegistry = 
					new Preprocessing.PreprocessingManagerExtentionsRegistry();

				Preprocessing.IPreprocessingStepsFactory preprocessingStepsFactory = new Preprocessing.PreprocessingStepsFactory(
					workspacesManager,
					pluggableProtocolManager,
					invokingSynchronization,
					preprocessingManagerExtensionsRegistry,
					progressAggregator
				);

				Preprocessing.ILogSourcesPreprocessingManager logSourcesPreprocessings = new Preprocessing.LogSourcesPreprocessingManager(
					invokingSynchronization,
					formatAutodetect,
					preprocessingStepsFactory,
					preprocessingManagerExtensionsRegistry
				) { Trace = tracer };

				IModel model = new Model(modelHost, tracer, invokingSynchronization, tempFilesManager, heartBeatTimer,
					filtersFactory, bookmarks, userDefinedFormatsManager, logProviderFactoryRegistry, storageManager,
					recentlyUsedLogs, logSourcesPreprocessings, logSourcesManager, colorGenerator, modelThreads, 
					preprocessingManagerExtensionsRegistry, progressAggregator);
				tracer.Info("model created");


				var presentersFacade = new UI.Presenters.Facade();
				UI.Presenters.IPresentersFacade navHandler = presentersFacade;
				
				UI.Presenters.LoadedMessages.IView loadedMessagesView = mainForm.loadedMessagesControl;
				UI.Presenters.LoadedMessages.IPresenter loadedMessagesPresenter = new UI.Presenters.LoadedMessages.Presenter(
					model,
					loadedMessagesView,
					navHandler,
					heartBeatTimer);

				UI.Presenters.LogViewer.IPresenter viewerPresenter = loadedMessagesPresenter.LogViewerPresenter;

				UI.Presenters.ITabUsageTracker tabUsageTracker = new UI.Presenters.TabUsageTracker();

				UI.StatusPopupsManager statusPopups = new UI.StatusPopupsManager(
					mainForm,
					mainForm.toolStripStatusLabel,
					heartBeatTimer);
				UI.Presenters.StatusReports.IPresenter statusReportFactory = statusPopups;

				UI.Presenters.Timeline.IPresenter timelinePresenter = new UI.Presenters.Timeline.Presenter(
					model,
					mainForm.timeLinePanel.TimelineControl,
					tracer,
					viewerPresenter,
					statusReportFactory,
					tabUsageTracker,
					heartBeatTimer);

				UI.Presenters.TimelinePanel.IPresenter timelinePanelPresenter = new UI.Presenters.TimelinePanel.Presenter(
					model,
					mainForm.timeLinePanel,
					timelinePresenter,
					heartBeatTimer);

				UI.Presenters.SearchResult.IPresenter searchResultPresenter = new UI.Presenters.SearchResult.Presenter(
					model,
					mainForm.searchResultView,
					navHandler,
					loadedMessagesPresenter,
					heartBeatTimer,
					filtersFactory);

				UI.Presenters.ThreadsList.IPresenter threadsListPresenter = new UI.Presenters.ThreadsList.Presenter(
					model, 
					mainForm.threadsListView,
					viewerPresenter,
					navHandler,
					viewUpdates,
					heartBeatTimer);
				tracer.Info("threads list presenter created");

				UI.Presenters.SearchPanel.IPresenter searchPanelPresenter = new UI.Presenters.SearchPanel.Presenter(
					model,
					mainForm.searchPanelView,
					new UI.SearchResultsPanelView() { container = mainForm.splitContainer_Log_SearchResults },
					viewerPresenter,
					searchResultPresenter,
					statusReportFactory);
				tracer.Info("search panel presenter created");

				UI.Presenters.SourcesList.IPresenter sourcesListPresenter = new UI.Presenters.SourcesList.Presenter(
					model,
					mainForm.sourcesListView.SourcesListView,
					logSourcesPreprocessings,
					new UI.Presenters.SourcePropertiesWindow.Presenter(new UI.SourceDetailsWindowView(), navHandler),
					viewerPresenter,
					navHandler);


				UI.LogsPreprocessorUI logsPreprocessorUI = new UI.LogsPreprocessorUI(
					logSourcesPreprocessings,
					mainForm,
					storageManager.GlobalSettingsEntry,
					statusPopups);

				UI.Presenters.Help.IPresenter helpPresenter = new UI.Presenters.Help.Presenter();

				CommandLineHandler commandLineHandler = new CommandLineHandler(
					logSourcesPreprocessings,
					preprocessingStepsFactory);

				UI.Presenters.SharingDialog.IPresenter sharingDialogPresenter = new UI.Presenters.SharingDialog.Presenter(
					logSourcesManager,
					workspacesManager,
					new UI.ShareDialog()
				);

				UI.Presenters.SourcesManager.IPresenter sourcesManagerPresenter = new UI.Presenters.SourcesManager.Presenter(
					model,
					mainForm.sourcesListView,
					logSourcesPreprocessings,
					preprocessingStepsFactory,
					workspacesManager,
					sourcesListPresenter,
					new UI.Presenters.NewLogSourceDialog.Presenter(
						model,
						new UI.NewLogSourceDialogView(model, commandLineHandler, helpPresenter),
						logsPreprocessorUI
					),
					heartBeatTimer,
					sharingDialogPresenter
				);


				UI.Presenters.MessagePropertiesDialog.IPresenter messagePropertiesDialogPresenter = new UI.Presenters.MessagePropertiesDialog.Presenter(
					model,
					new MessagePropertiesDialogView(mainForm),
					viewerPresenter,
					navHandler);


				Func<IFiltersList, UI.FiltersManagerView, UI.Presenters.FiltersManager.IPresenter> createFiltersManager = (filters, view) =>
				{
					var dialogPresenter = new UI.Presenters.FilterDialog.Presenter(model, filters, new UI.FilterDialogView(filtersFactory));
					UI.Presenters.FiltersListBox.IPresenter listPresenter = new UI.Presenters.FiltersListBox.Presenter(model, filters, view.FiltersListView, dialogPresenter);
					UI.Presenters.FiltersManager.IPresenter managerPresenter = new UI.Presenters.FiltersManager.Presenter(
						model, filters, view, listPresenter, dialogPresenter, viewerPresenter, viewUpdates, heartBeatTimer, filtersFactory);
					return managerPresenter;
				};

				UI.Presenters.FiltersManager.IPresenter displayFiltersManagerPresenter = createFiltersManager(
					model.DisplayFilters,
					mainForm.displayFiltersManagementView);

				UI.Presenters.FiltersListBox.IPresenter filtersListPresenter = displayFiltersManagerPresenter.FiltersListPresenter;

				UI.Presenters.FiltersManager.IPresenter hlFiltersManagerPresenter = createFiltersManager(
					model.HighlightFilters,
					mainForm.hlFiltersManagementView);

				UI.Presenters.BookmarksList.IPresenter bookmarksListPresenter = new UI.Presenters.BookmarksList.Presenter(
					model, 
					mainForm.bookmarksManagerView.ListView,
					heartBeatTimer,
					loadedMessagesPresenter);

				UI.Presenters.BookmarksManager.IPresenter bookmarksManagerPresenter = new UI.Presenters.BookmarksManager.Presenter(
					model,
					mainForm.bookmarksManagerView,
					viewerPresenter,
					searchResultPresenter,
					bookmarksListPresenter,
					tracer,
					statusReportFactory,
					navHandler,
					viewUpdates);

				AutoUpdate.IAutoUpdater autoUpdater = new AutoUpdate.AutoUpdater(
					instancesCounter,
					new AutoUpdate.AzureUpdateDownloader(),
					tempFilesManager,
					model,
					invokingSynchronization
				);


				var unhandledExceptionsReporter = new Telemetry.UnhandledExceptionsReporter(
					telemetryCollector
				);

				UI.Presenters.Options.Dialog.IPresenter optionsDialogPresenter = new UI.Presenters.Options.Dialog.Presenter(
					model,
					new OptionsDialogView(),
					pageView => new UI.Presenters.Options.MemAndPerformancePage.Presenter(model, pageView),
					pageView => new UI.Presenters.Options.Appearance.Presenter(model, pageView),
					pageView => new UI.Presenters.Options.UpdatesAndFeedback.Presenter(autoUpdater, model.GlobalSettings, pageView)
				);

				DragDropHandler dragDropHandler = new DragDropHandler(
					logSourcesPreprocessings, 
					preprocessingStepsFactory,
					model);

				UI.Presenters.MainForm.IPresenter mainFormPresenter = new UI.Presenters.MainForm.Presenter(
					model,
					mainForm,
					tracer,
					viewerPresenter,
					searchResultPresenter,
					searchPanelPresenter,
					sourcesListPresenter,
					sourcesManagerPresenter,
					timelinePresenter,
					messagePropertiesDialogPresenter,
					loadedMessagesPresenter,
					commandLineHandler,
					bookmarksManagerPresenter,
					heartBeatTimer,
					tabUsageTracker,
					statusReportFactory,
					dragDropHandler,
					navHandler,
					optionsDialogPresenter,
					autoUpdater,
					progressAggregator);
				tracer.Info("main form presenter created");

				((AppShutdown)shutdown).Attach(mainFormPresenter);

				LogJointApplication pluginEntryPoint = new LogJointApplication(
					model,
					mainForm,
					loadedMessagesPresenter,
					filtersListPresenter,
					bookmarksManagerPresenter,
					sourcesManagerPresenter,
					presentersFacade,
					invokingSynchronization,
					telemetryCollector);

				PluginsManager pluginsManager = new PluginsManager(
					tracer,
					pluginEntryPoint,
					mainForm.menuTabControl,
					mainFormPresenter);
				tracer.Info("plugin manager created");

				modelHost.Init(viewerPresenter, viewUpdates);
				presentersFacade.Init(
					messagePropertiesDialogPresenter,
					threadsListPresenter,
					sourcesListPresenter,
					bookmarksManagerPresenter,
					mainFormPresenter);

				return mainForm;
			}
		}

		static LJTraceSource CreateTracer()
		{
			LJTraceSource tracer = LJTraceSource.EmptyTracer;
			try
			{
				tracer = new LJTraceSource("TraceSourceApp");
			}
			catch (Exception)
			{
				Debug.Write("Failed to create tracer");
			}
			return tracer;
		}
	}
}