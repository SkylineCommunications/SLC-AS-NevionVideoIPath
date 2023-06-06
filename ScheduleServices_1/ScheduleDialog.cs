namespace ScheduleServices_1
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.DeveloperCommunityLibrary.InteractiveAutomationToolkit;

	public class ScheduleDialog : Dialog
	{
		private readonly Label sourceNameLabel = new Label("Source Name");
		private readonly Label sourceNameValue = new Label();
		private readonly Label destinationNamesLabel = new Label("Destination(s)");
		private readonly Label destinationNameValues = new Label();

		private readonly Label profileLabel = new Label("Profiles");
		private readonly DropDown profileDropDown = new DropDown();
		private readonly Label serviceNameLabel = new Label("Service Name");
		private readonly TextBox serviceNameTextBox = new TextBox();
		private readonly Label descriptionLabel = new Label("Description");
		private readonly TextBox descriptionTextBox = new TextBox();
		private readonly Label tagLabel = new Label("Tag");
		private readonly TextBox tagTextBox = new TextBox();

		private readonly Label startTimeLabel = new Label("Start");
		private readonly DateTimePicker startTime = new DateTimePicker(DateTime.Now);
		private readonly Label endTimeLabel = new Label("End");
		private readonly DateTimePicker endTime = new DateTimePicker(DateTime.Now.AddHours(1));

		private readonly Element nevionVideoIPathElement;

		private List<string> destinationNames;

		public ScheduleDialog(IEngine engine) : base(engine)
		{
			Title = "Schedule Services";

			nevionVideoIPathElement = Engine.FindElement("Nevion iPath (Lab)");
			ConnectButton.Pressed += (s, o) => TriggerConnectOnElement();

			InitializeProfiles();
			GenerateUI();
		}

		public string SourceName
		{
			get
			{
				return sourceNameValue.Text;
			}

			private set
			{
				sourceNameValue.Text = value;
			}
		}

		/// <summary>
		/// Gets the destination names, in case of multiple names the string is comma separated (",") for each element.
		/// </summary>
		public List<string> DestinationNames
		{
			get
			{
				return destinationNames;
			}

			private set
			{
				destinationNames = value;
				destinationNameValues.Text = String.Join(",", destinationNames);
			}
		}

		public Button ConnectButton { get; private set; } = new Button("Connect");

		public void SetSourceAndDestinationNames(string sourceName, List<string> destinationNames)
		{
			SourceName = sourceName;
			DestinationNames = destinationNames;
		}

		public void TriggerConnectOnElement()
		{
			try
			{
				if (nevionVideoIPathElement == null || !nevionVideoIPathElement.IsActive || string.IsNullOrWhiteSpace(SourceName) || !DestinationNames.Any())
				{
					Engine.ExitSuccess("ScheduleDialog|TriggerConnectOnElement|Not Allowed");
					return;
				}

				string route = DestinationNames.Count > 1 ? "Point-to-Multipoint" : "Point-to-Point";

				var concatenatedDestnationNames = string.Join(",", DestinationNames);
				string visioString = string.Join(";", profileDropDown.Selected, serviceNameTextBox.Text, SourceName, concatenatedDestnationNames, Convert.ToString(startTime.DateTime.ToOADate(), CultureInfo.InvariantCulture), Convert.ToString(endTime.DateTime.ToOADate(), CultureInfo.InvariantCulture), route, 0, descriptionTextBox.Text, tagTextBox.Text);

				nevionVideoIPathElement.SetParameter(2309, visioString);
			}
			catch (Exception e)
			{
				Engine.Log($"ScheduleServices|TriggerConnectOnElement|Connecting current services failed due to: {e}");
			}
		}

		private void GenerateUI()
		{
			int row = -1;

			AddWidget(serviceNameLabel, ++row, 0);
			AddWidget(serviceNameTextBox, row, 1);

			AddWidget(profileLabel, ++row, 0);
			AddWidget(profileDropDown, row, 1);

			AddWidget(sourceNameLabel, ++row, 0);
			AddWidget(sourceNameValue, row, 1);

			AddWidget(destinationNamesLabel, ++row, 0);
			AddWidget(destinationNameValues, row, 1);

			AddWidget(startTimeLabel, ++row, 0);
			AddWidget(startTime, row, 1);
			AddWidget(endTimeLabel, ++row, 0);
			AddWidget(endTime, row, 1);

			AddWidget(tagLabel, ++row, 0);
			AddWidget(tagTextBox, row, 1);

			AddWidget(descriptionLabel, ++row, 0);
			AddWidget(descriptionTextBox, row, 1);

			row += row + 4;

			AddWidget(new WhiteSpace(), ++row, 0);

			AddWidget(ConnectButton, ++row, 0);
		}

		private void InitializeProfiles()
		{
			var profileNames = nevionVideoIPathElement.GetTableDisplayKeys(2400);
			if (profileNames != null && profileNames.Any())
			{
				profileDropDown.SetOptions(profileNames);
				profileDropDown.Selected = profileNames.FirstOrDefault();
			}
		}
	}
}
