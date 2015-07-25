using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Linq;

namespace LogJoint.UI.Presenters.SourcesManager
{
	public interface IPresenter
	{
		event EventHandler<BusyStateEventArgs> OnBusyState;
		event EventHandler OnViewUpdated;
	};

	public interface IView
	{
		void SetPresenter(IViewEvents presenter);
		bool ShowDeletionConfirmationDialog(int nrOfSourcesToDelete);
		void ShowMRUMenu(List<MRUMenuItem> items);
		void ShowMRUOpeningFailurePopup();
		void EnableDeleteAllSourcesButton(bool enable);
		void EnableDeleteSelectedSourcesButton(bool enable);
		void EnableTrackChangesCheckBox(bool enable);
		void SetTrackingChangesCheckBoxState(TrackingChangesCheckBoxState state);
		void SetShareButtonState(bool visible, bool enabled);
	};

	public struct MRUMenuItem
	{
		public object Data;
		public string Text;
		public bool Disabled;
	};

	public enum TrackingChangesCheckBoxState
	{
		Indeterminate,
		Checked,
		Unchecked
	};

	public interface IViewEvents
	{
		void OnAddNewLogButtonClicked();
		void OnDeleteSelectedLogSourcesButtonClicked();
		void OnDeleteAllLogSourcesButtonClicked();
		void OnMRUButtonClicked();
		void OnMRUMenuItemClicked(object data);
		void OnTrackingChangesCheckBoxChecked(bool value);
		void OnShareButtonClicked();
	};
};