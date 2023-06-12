/*
****************************************************************************
*  Copyright (c) 2023,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

dd/mm/2023	1.0.0.1		XXX, Skyline	Initial version
****************************************************************************
*/

namespace ScheduleServices_1
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.DeveloperCommunityLibrary.InteractiveAutomationToolkit;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	///
	public class Script
	{
		private static ScheduleDialog scheduleDialog;

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public static void Run(IEngine engine)
		{
			try
			{
				//engine.ShowUI();

				string sourceDescriptorLabelInputParameter = engine.GetScriptParam("SourceName").Value;
				if (!TryGetInputValue(sourceDescriptorLabelInputParameter, out List<string> sourceNames))
				{
					engine.Log("Schedule Services|Failed to gather Source Name");
					return;
				}

				string destinationDescriptorLabelInputParameter = engine.GetScriptParam("DestinationNames").Value;
				if (!TryGetInputValue(destinationDescriptorLabelInputParameter, out List<string> destinationNames))
				{
					engine.Log("Schedule Services|Failed to gather Destination Names");
					return;
				}

				string profileInputParameter = engine.GetScriptParam("Profile").Value;
				if (!TryGetInputValue(profileInputParameter, out List<string> profile))
				{
					engine.Log("Schedule Services|Failed to gather selected profile");
					return;
				}

				Initialize(engine, sourceNames.FirstOrDefault(), destinationNames, profile.FirstOrDefault());

				var controller = new InteractiveController(engine);
				controller.Run(scheduleDialog);
			}
			catch (Exception e)
			{
				engine.Log($"Schedule Services|Run|Something went wrong while disconnecting services {e}");
			}
		}

		private static void Initialize(IEngine engine, string sourceName, List<string> destinationNames, string selectedProfile)
		{
			scheduleDialog = new ScheduleDialog(engine);
			scheduleDialog.SetInitialValues(sourceName, destinationNames, selectedProfile);
			scheduleDialog.ConnectButton.Pressed += (s, o) => engine.ExitSuccess("Triggered connect to nevion IPath driver to create a custom service connection");
		}

		private static bool TryGetInputValue(string input, out List<string> labels)
		{
			labels = new List<string>();

			try
			{
				labels = JsonConvert.DeserializeObject<List<string>>(input);

				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}
	}
}