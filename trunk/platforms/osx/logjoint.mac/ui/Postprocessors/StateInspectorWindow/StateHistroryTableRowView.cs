﻿using System;
using MonoMac.AppKit;
using System.Drawing;
using LogJoint.UI;
using LogJoint.Drawing;
using LJD = LogJoint.Drawing;
using MonoMac.Foundation;

namespace LogJoint.UI.Postprocessing.StateInspector
{
	public class StateHistroryTableRowView: NSTableRowView
	{
		public StateInspectorWindowController owner;
		public int row;
		static LJD.Image bookmarkImage = new LJD.Image(NSImage.ImageNamed("Bookmark.png"));

		public override void DrawBackground(RectangleF dirtyRect)
		{
			base.DrawBackground(dirtyRect);
			DrawFocusedMessage();
			DrawBookmark();
		}

		public override void DrawSelection(RectangleF dirtyRect)
		{
			base.DrawSelection(dirtyRect);
			DrawFocusedMessage();
			DrawBookmark();
		}

		void DrawFocusedMessage()
		{
			Tuple<int, int> focused = owner.EventsHandler.OnDrawFocusedMessageMark();
			if (focused != null)
			{
				var frame = this.Frame;
				float y;
				float itemH = frame.Height;
				SizeF markSize = UIUtils.FocusedItemMarkFrame.Size;
				if (focused.Item1 != focused.Item2)
					y = itemH * focused.Item1 + itemH / 2;
				else
					y = itemH * focused.Item1;
				if (Math.Abs(y) < .01f)
					y = markSize.Height / 2;
				y -= frame.Y;
				using (var g = new LogJoint.Drawing.Graphics())
				{
					UIUtils.DrawFocusedItemMark(g, 
						owner.HistoryTableView.GetCellFrame(1, row).Left - 2, y, drawOuterFrame: true);
				}
			}
		}

		void DrawBookmark()
		{
			bool bookmarked = owner.EventsHandler.OnGetHistoryItemBookmarked(owner.StateHistoryDataSource.data[row]);
			if (bookmarked)
			{
				var frame = this.Frame;
				float itemH = frame.Height;
				using (var g = new LogJoint.Drawing.Graphics())
				{
					var sz = bookmarkImage.GetSize(width: 9);
					g.DrawImage(bookmarkImage, new RectangleF(new PointF(
						owner.HistoryTableView.GetCellFrame(0, row).Left + 1,
						itemH * row + itemH / 2 - frame.Y - sz.Height / 2), sz));
				}
			}
		}

		[Export ("insertText:")]
		void OnInsertText (NSObject theEvent)
		{
			var s = theEvent.ToString();
			if (s == "b" || s == "B")
			{
				s.ToLower();
				//owner.OnKeyEvent(Key.Bookmark);
			}
		}

		public override bool AcceptsFirstResponder ()
		{
			return true;
		}
	}
}

