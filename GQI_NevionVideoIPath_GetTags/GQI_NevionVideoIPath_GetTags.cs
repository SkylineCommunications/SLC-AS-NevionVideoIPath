using System;
using System.Collections.Generic;
using System.Linq;
using Skyline.DataMiner.Analytics.GenericInterface;
using Skyline.DataMiner.Net.Messages;

[GQIMetaData(Name = "Nevion VideoIPath Get Tags")]
public class GQI_NevionVideoIPath_GetTags : IGQIDataSource, IGQIOnInit, IGQIInputArguments
{
	private GQIDMS dms;

	private GQIIntArgument typeArgument = new GQIIntArgument("Type") { IsRequired = true };
	private Type type;

	private GQIStringArgument elementIdArgument = new GQIStringArgument("NevionElementId") { IsRequired = false };
	private string nevionElementId;

	private GQIStringArgument profileArgument = new GQIStringArgument("Profile") { IsRequired = false };
	private string profile;

	private int dataminerId;
	private int elementId;

	private enum Type
	{
		Source = 0,
		Destination = 1,
	}

	public GQIColumn[] GetColumns()
	{
		return new GQIColumn[]
		{
			new GQIStringColumn("Tag"),
		};
	}

	public GQIArgument[] GetInputArguments()
	{
		return new GQIArgument[] { typeArgument, elementIdArgument, profileArgument };
	}

	public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
	{
		type = (Type)args.GetArgumentValue(typeArgument);
		nevionElementId = Convert.ToString(args.GetArgumentValue(elementIdArgument));
		profile = Convert.ToString(args.GetArgumentValue(profileArgument));

		GetNevionVideoIPathElement();
		return new OnArgumentsProcessedOutputArgs();
	}

	public GQIPage GetNextPage(GetNextPageInputArgs args)
	{
		var profileTagsFilter = GetTagsForProfile();
		var tags = GetTags(profileTagsFilter);

		var rows = new List<GQIRow>();
		foreach (var tag in tags.OrderBy(t => t))
		{
			var row = new GQIRow(new GQICell[] { new GQICell() { Value = tag } });
			rows.Add(row);
		}

		return new GQIPage(rows.ToArray())
		{
			HasNextPage = false,
		};
	}

	public OnInitOutputArgs OnInit(OnInitInputArgs args)
	{
		dms = args.DMS;

		return new OnInitOutputArgs();
	}

	private void GetNevionVideoIPathElement()
	{
		dataminerId = -1;
		elementId = -1;

		if (!string.IsNullOrWhiteSpace(nevionElementId))
		{
			GetNevionVideoIPathSpecificElement();
		}
		else
		{
			GetFirstNevionVideoIPathProductionElement();
		}
	}

	private void GetNevionVideoIPathSpecificElement()
	{
		var elementIdSplit = nevionElementId.Split('/');

		if (elementIdSplit.Length != 2)
		{
			return;
		}

		var checkDmaId = Convert.ToInt32(elementIdSplit[0]);
		var checkElementId = Convert.ToInt32(elementIdSplit[1]);

		var infoMessage = new GetElementByIDMessage
		{
			DataMinerID = checkDmaId,
			ElementID = checkElementId,
		};

		var infoMessageResponse = dms.SendMessage(infoMessage) as ElementInfoEventMessage;

		if (infoMessageResponse == null)
		{
			return;
		}

		dataminerId = checkDmaId;
		elementId = checkElementId;
	}

	private void GetFirstNevionVideoIPathProductionElement()
	{
		var infoMessage = new GetInfoMessage { Type = InfoType.ElementInfo };
		var infoMessageResponses = dms.SendMessages(infoMessage);
		foreach (var response in infoMessageResponses)
		{
			var elementInfoEventMessage = (ElementInfoEventMessage)response;
			if (elementInfoEventMessage == null)
			{
				continue;
			}

			if (elementInfoEventMessage?.Protocol == "Nevion Video iPath" && elementInfoEventMessage?.ProtocolVersion == "Production")
			{
				dataminerId = elementInfoEventMessage.DataMinerID;
				elementId = elementInfoEventMessage.ElementID;
				break;
			}
		}
	}

	private HashSet<string> GetTagsForProfile()
	{
		var columns = GetProfilesTableColumns();
		if (!columns.Any())
		{
			return new HashSet<string>();
		}

		var profileTags = new HashSet<string>();

		for (int i = 0; i < columns[1].ArrayValue.Length; i++)
		{
			var profileNameCell = columns[1].ArrayValue[i];
			if (profileNameCell.IsEmpty)
			{
				continue;
			}

			var profileName = profileNameCell.CellValue.StringValue;
			if (profileName != profile)
			{
				continue;
			}

			var profileTagsCell = columns[3].ArrayValue[i];
			if (profileTagsCell.IsEmpty)
			{
				break;
			}

			var valueTags = profileTagsCell.CellValue.StringValue.Split(',');
			foreach (var valueTag in valueTags)
			{
				if (String.IsNullOrEmpty(valueTag))
				{
					continue;
				}

				profileTags.Add(valueTag.Trim());
			}

			break;
		}

		return profileTags;
	}

	private ParameterValue[] GetProfilesTableColumns()
	{
		var tableId = 2400;
		var getPartialTableMessage = new GetPartialTableMessage(dataminerId, elementId, tableId, new[] { "forceFullTable=true" });
		var parameterChangeEventMessage = (ParameterChangeEventMessage)dms.SendMessage(getPartialTableMessage);
		if (parameterChangeEventMessage.NewValue?.ArrayValue == null)
		{
			return new ParameterValue[0];
		}

		var columns = parameterChangeEventMessage.NewValue.ArrayValue;
		if (columns.Length < 4)
		{
			return new ParameterValue[0];
		}

		return columns;
	}

	private HashSet<string> GetTags(HashSet<string> profileTagsFilter)
	{
		var columns = GetTagsTableColumns();
		if (!columns.Any())
		{
			return new HashSet<string>();
		}

		var tags = new HashSet<string>();

		foreach (var tagsCell in columns[3].ArrayValue)
		{
			if (tagsCell.IsEmpty)
			{
				continue;
			}

			if (String.IsNullOrEmpty(tagsCell.CellValue.StringValue))
			{
				continue;
			}

			var valueTags = tagsCell.CellValue.StringValue.Split(',');
			foreach (var valueTag in valueTags)
			{
				if (String.IsNullOrEmpty(valueTag))
				{
					continue;
				}

				tags.Add(valueTag.Trim());
			}
		}

		if (profileTagsFilter != null && profileTagsFilter.Any())
		{
			tags.IntersectWith(profileTagsFilter);
		}

		return tags;
	}

	private ParameterValue[] GetTagsTableColumns()
	{
		var tableId = type == Type.Source ? 1300 : 1400;
		var getPartialTableMessage = new GetPartialTableMessage(dataminerId, elementId, tableId, new[] { "forceFullTable=true" });
		var parameterChangeEventMessage = (ParameterChangeEventMessage)dms.SendMessage(getPartialTableMessage);
		if (parameterChangeEventMessage.NewValue?.ArrayValue == null)
		{
			return new ParameterValue[0];
		}

		var columns = parameterChangeEventMessage.NewValue.ArrayValue;
		if (columns.Length < 4)
		{
			return new ParameterValue[0];
		}

		return columns;
	}
}