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
	using System.Linq;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

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

				var sourceDescriptorLabelInputParameter = engine.GetScriptParam("SourceName").Value;
				if (!TryGetInputValue(sourceDescriptorLabelInputParameter, out List<string> sourceNames))
				{
					engine.ExitFail("Invalid source!");
					return;
				}

				if (sourceNames.Count != 1)
				{
					engine.ExitFail("Only 1 source should be selected!");
					return;
				}

				var sourceName = sourceNames.FirstOrDefault();
				if (String.IsNullOrEmpty(sourceName))
				{
					engine.ExitFail("Invalid source!");
					return;
				}

				var destinationDescriptorLabelInputParameter = engine.GetScriptParam("DestinationNames").Value;
				if (!TryGetInputValue(destinationDescriptorLabelInputParameter, out List<string> destinationNames))
				{
					engine.ExitFail("Invalid destinations!");
					return;
				}

				if (destinationNames.Count < 1)
				{
					engine.ExitFail("No destinations selected!");
					return;
				}

				var profileInputParameter = engine.GetScriptParam("Profile").Value;
				if (!TryGetInputValue(profileInputParameter, out List<string> profileNames))
				{
					engine.ExitFail("Invalid profile!");
					return;
				}

				if (profileNames.Count != 1)
				{
					engine.ExitFail("Only 1 profile should be selected!");
					return;
				}

				var profileName = profileNames.FirstOrDefault();
				if (String.IsNullOrEmpty(profileName))
				{
					engine.ExitFail("Invalid profile!");
					return;
				}

				Initialize(engine, sourceName, destinationNames, profileName);

				var controller = new InteractiveController(engine);
				controller.Run(scheduleDialog);
			}
			catch (ScriptAbortException)
			{
				// do nothing
			}
			catch (Exception e)
			{
				engine.Log($"Schedule failed: {e}");
				engine.ExitFail("Schedule failed due to unknown exception!");
			}
		}

		private static void Initialize(IEngine engine, string sourceName, List<string> destinationNames, string profile)
		{
			scheduleDialog = new ScheduleDialog(engine);
			scheduleDialog.SetInput(sourceName, destinationNames, profile);
			scheduleDialog.ConnectButton.Pressed += (s, o) => engine.ExitSuccess(String.Empty);
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