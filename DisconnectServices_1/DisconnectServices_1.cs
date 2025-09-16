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

namespace DisconnectServices_1
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private static string[] primaryKeysCurrentServices = new string[0];
		private static Element nevionVideoIPathElement;

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public static void Run(IEngine engine)
		{
			try
			{
				//engine.ShowUI();

				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				// throw; // Comment if it should be treated as a normal exit of the script.
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
				throw;
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
				throw;
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
				throw;
			}
			catch (Exception e)
			{
				engine.ExitFail("Run|Something went wrong: " + e);
			}
		}

		/// <summary>
		/// The script safe entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public static void RunSafe(IEngine engine)
		{
			engine.SetFlag(RunTimeFlags.NoKeyCaching);

			bool isInteractive = true;

			var destinationIdsParameter = engine.GetScriptParam("DestinationIds").Value;
			if (!TryGetIdsFromInput(destinationIdsParameter, out List<string> destinationIds))
			{
				GenerateErrorMessageDialog(engine, "Invalid destinations!", isInteractive);
				return;
			}

			if (destinationIds.Count <= 0)
			{
				GenerateErrorMessageDialog(engine, "Please select a destination!", isInteractive);
				return;
			}

			nevionVideoIPathElement = GetNevionVideoIPathElement(engine);
			if (nevionVideoIPathElement == null)
			{
				GenerateErrorMessageDialog(engine, "Nevion Video iPath element not found!", isInteractive);
				return;
			}

			if (!nevionVideoIPathElement.IsActive)
			{
				GenerateErrorMessageDialog(engine, "Nevion Video iPath element not active!", isInteractive);
				return;
			}

			primaryKeysCurrentServices = nevionVideoIPathElement.GetTablePrimaryKeys(1500); // Used to check if new connection entries has been added after the ConnectServices.

			DisconnectDestinations(engine, destinationIds);
			if(!VerifyDisconnectService(engine, destinationIds))
			{
				GenerateErrorMessageDialog(engine, "Unable to disconnect.", isInteractive);
			}
			else
			{
				GenerateSuccessMessageDialog(engine, "Disconnected!", isInteractive);
			}
		}

		private static Element GetNevionVideoIPathElement(IEngine engine)
		{
			TryGetInputValue(engine.GetScriptParam("NevionElementId").Value, out var elementIds);
			var nevionElementId = elementIds.FirstOrDefault();

			if (!string.IsNullOrWhiteSpace(nevionElementId))
			{
				return GetNevionVideoIPathSpecificElement(engine, nevionElementId);
			}
			else
			{
				return GetFirstNevionVideoIPathProductionElement(engine);
			}
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

		private static Element GetNevionVideoIPathSpecificElement(IEngine engine, string nevionElementId)
		{
			var elementIdSplit = nevionElementId.Split('/');

			if (elementIdSplit.Length != 2)
			{
				return null;
			}

			var checkDmaId = Convert.ToInt32(elementIdSplit[0]);
			var checkElementId = Convert.ToInt32(elementIdSplit[1]);

			return engine.FindElement(checkDmaId, checkElementId);
		}

		private static Element GetFirstNevionVideoIPathProductionElement(IEngine engine)
		{
			return engine.FindElementsByProtocol("Nevion Video iPath", "Production").FirstOrDefault();
		}

		private static void DisconnectDestinations(IEngine engine, List<string> destinationIds)
		{
			var nevionVideoIPathElement = engine.FindElementsByProtocol("Nevion Video iPath", "Production").FirstOrDefault();
			if (nevionVideoIPathElement == null)
			{
				engine.ExitFail("Nevion Video iPath element not found!");
				return;
			}

			if (!nevionVideoIPathElement.IsActive)
			{
				engine.ExitFail("Nevion Video iPath element not active!");
				return;
			}

			var dms = engine.GetDms();

			var nevionVideoIPathDmsElement = dms.GetElement(nevionVideoIPathElement.ElementName);
			var currentServicesTable = nevionVideoIPathDmsElement.GetTable(1500);

			var servicesToCancel = new List<string>();
			foreach (var destinationId in destinationIds)
			{
				var rows = currentServicesTable.QueryData(new[] { new ColumnFilter { Pid = 1508, ComparisonOperator = ComparisonOperator.Equal, Value = destinationId } });
				if (rows.Any())
				{
					servicesToCancel.AddRange(rows.Select(r => Convert.ToString(r[0])));
				}
			}

			foreach (var serviceId in servicesToCancel)
			{
				nevionVideoIPathElement.SetParameterByPrimaryKey(1515, serviceId, 1);
			}
		}

		private static bool TryGetIdsFromInput(string input, out List<string> ids)
		{
			ids = new List<string>();

			try
			{
				ids = JsonConvert.DeserializeObject<List<string>>(input);

				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		private static bool VerifyDisconnectService(IEngine engine, List<string> destinationNames)
		{
			int retries = 0;
			bool allEntriesFound = false;
			int tableEntriesExcludingCurrentDestination = primaryKeysCurrentServices.Length - destinationNames.Count;
			while (!allEntriesFound && retries < 100)
			{
				engine.Sleep(60);

				var allPrimaryKeys = nevionVideoIPathElement.GetTablePrimaryKeys(1500);

				allEntriesFound = allPrimaryKeys.Length == tableEntriesExcludingCurrentDestination;

				retries++;
			}

			return allEntriesFound;
		}

		private static void GenerateSuccessMessageDialog(IEngine engine, string message, bool isInteractive)
		{
			if (isInteractive)
			{
				var successMessageDialog = new MessageDialog(engine, message);
				successMessageDialog.Title = "Disconnect Services";
				successMessageDialog.Interacted += (object sender, EventArgs e) =>
				{
					engine.ExitSuccess(message);
				};

				var controller = new InteractiveController(engine);
				controller.Run(successMessageDialog);
			}
			else
			{
				engine.ExitSuccess(message);
			}
		}

		private static void GenerateErrorMessageDialog(IEngine engine, string message, bool isInteractive)
		{
			if (isInteractive)
			{
				var errorMessageDialog = new MessageDialog(engine, message);
				errorMessageDialog.Title = "Disconnect Services";
				errorMessageDialog.Interacted += (object sender, EventArgs e) =>
				{
					engine.ExitSuccess(message);
				};

				var controller = new InteractiveController(engine);
				controller.Run(errorMessageDialog);
			}
			else
			{
				engine.ExitFail(message);
			}
		}
	}
}