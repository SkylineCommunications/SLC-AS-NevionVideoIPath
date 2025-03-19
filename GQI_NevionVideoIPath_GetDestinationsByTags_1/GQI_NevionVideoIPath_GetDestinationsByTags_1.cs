using System;
using System.Collections.Generic;
using System.Linq;
using Skyline.DataMiner.Analytics.GenericInterface;
using Skyline.DataMiner.Net.Messages;

[GQIMetaData(Name = "Nevion VideoIPath Get Destinations By Tags")]
public class GQI_NevionVideoIPath_GetDestinationsByTags : IGQIDataSource, IGQIOnInit, IGQIInputArguments
{
	private GQIDMS dms;

	private GQIStringArgument profileArgument = new GQIStringArgument("Profile") { IsRequired = false };
	private string profile;

	private GQIStringArgument tagArgument = new GQIStringArgument("Tags") { IsRequired = false };
	private string tag;

	private int dataminerId;
	private int elementId;

	public GQIColumn[] GetColumns()
	{
		return new GQIColumn[]
		{
			new GQIStringColumn("Name"),
			new GQIStringColumn("Connected Source"),
			new GQIStringColumn("Tags"),
			new GQIStringColumn("ID"),
			new GQIBooleanColumn("Is Connected"),
		};
	}

	public GQIArgument[] GetInputArguments()
	{
		return new GQIArgument[] { profileArgument, tagArgument };
	}

	public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
	{
		profile = args.GetArgumentValue<string>(profileArgument);
		tag = args.GetArgumentValue<string>(tagArgument);
		return new OnArgumentsProcessedOutputArgs();
	}

	public GQIPage GetNextPage(GetNextPageInputArgs args)
	{
		if (dataminerId == -1 || elementId == -1)
		{
			return new GQIPage(new GQIRow[0])
			{
				HasNextPage = false,
			};
		}

		List<GQIRow> rows;
		if (!String.IsNullOrEmpty(tag))
		{
			rows = GetDestinationByTagRows(tag);
		}
		else if (!String.IsNullOrEmpty(profile))
		{
			var tagFilter = GetTagsForProfile();
			rows = GetDestinationByTagRows(tagFilter.ToArray());
		}
		else
		{
			rows = GetDestinationByTagRows();
		}

		return new GQIPage(rows.ToArray())
		{
			HasNextPage = false,
		};
	}

	public OnInitOutputArgs OnInit(OnInitInputArgs args)
	{
		dms = args.DMS;
		GetNevionVideoIPathElement();

		return new OnInitOutputArgs();
	}

	private void GetNevionVideoIPathElement()
	{
		dataminerId = -1;
		elementId = -1;

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

	private List<GQIRow> GetDestinationByTagRows(params string[] tagFilter)
	{
		var columns = GetDestinationTableColumns();
		if (!columns.Any())
		{
			return new List<GQIRow>();
		}

		var destinationIdSourceNameRelations = GetCurrentServicesDestinationIdToSourceNameRelationDictionary();

		return ProcessDestinationByTagTable(columns, destinationIdSourceNameRelations, tagFilter);
	}

	private ParameterValue[] GetDestinationTableColumns()
	{
		var getPartialTableMessage = new GetPartialTableMessage(dataminerId, elementId, 1400, new[] { "forceFullTable=true" });
		var parameterChangeEventMessage = (ParameterChangeEventMessage)dms.SendMessage(getPartialTableMessage);
		if (parameterChangeEventMessage.NewValue?.ArrayValue == null)
		{
			return new ParameterValue[0];
		}

		var columns = parameterChangeEventMessage.NewValue.ArrayValue;
		if (columns.Length < 6)
		{
			return new ParameterValue[0];
		}

		return columns;
	}

	private List<GQIRow> ProcessDestinationByTagTable(ParameterValue[] columns, Dictionary<string, string> destinationIdToSourceNameRelations, params string[] tagFilter)
	{
		var rows = new List<GQIRow>();
		for (int i = 0; i < columns[0].ArrayValue.Length; i++)
		{
			var destinationRow = new DestinationByTagRow(columns, i, destinationIdToSourceNameRelations);
			if (!destinationRow.IsValid())
			{
				continue;
			}

			if (!destinationRow.MatchesTagFilter(tagFilter))
			{
				continue;
			}

			rows.Add(destinationRow.ToGqiRow());
		}

		return rows;
	}

	private Dictionary<string, string> GetCurrentServicesDestinationIdToSourceNameRelationDictionary()
	{
		var columns = GetCurrentServicesTableColumns();
		if (!columns.Any())
		{
			return new Dictionary<string, string>();
		}

		var destinationIdToSourceNameDic = new Dictionary<string, string>();

		for (int i = 0; i < columns[7].ArrayValue.Length; i++)
		{
			var destinationIdCell = columns[7].ArrayValue[i];
			if (destinationIdCell.IsEmpty)
			{
				continue;
			}

			var sourceNameCell = columns[2].ArrayValue[i];
			if (sourceNameCell.IsEmpty)
			{
				continue;
			}

			if (!destinationIdToSourceNameDic.ContainsKey(destinationIdCell.CellValue.StringValue))
			{
				destinationIdToSourceNameDic.Add(destinationIdCell.CellValue.StringValue, sourceNameCell.CellValue.StringValue);
			}
		}

		return destinationIdToSourceNameDic;
	}

	private ParameterValue[] GetCurrentServicesTableColumns()
	{
		var tableId = 1500;
		var getPartialTableMessage = new GetPartialTableMessage(dataminerId, elementId, tableId, new[] { "forceFullTable=true" });
		var parameterChangeEventMessage = (ParameterChangeEventMessage)dms.SendMessage(getPartialTableMessage);
		if (parameterChangeEventMessage.NewValue?.ArrayValue == null)
		{
			return new ParameterValue[0];
		}

		var columns = parameterChangeEventMessage.NewValue.ArrayValue;
		if (columns.Length < 8)
		{
			return new ParameterValue[0];
		}

		return columns;
	}
}

public class DestinationByTagRow
{
	public DestinationByTagRow(ParameterValue[] columns, int row, Dictionary<string,string> destinationIdToSourceNameRelations)
	{
		var nameCell = columns[0].ArrayValue[row];
		Name = !nameCell.IsEmpty ? nameCell.CellValue.StringValue : String.Empty;

		var idCell = columns[1].ArrayValue[row];
		Id = !idCell.IsEmpty ? idCell.CellValue.StringValue : String.Empty;

		var tagsCell = columns[3].ArrayValue[row];
		if (!tagsCell.IsEmpty)
		{
			Tags = tagsCell.CellValue.StringValue
				.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(t => t.Trim())
				.ToArray();
		}

		var descriptionCell = columns[2].ArrayValue[row];
		Description = !descriptionCell.IsEmpty ? descriptionCell.CellValue.StringValue : String.Empty;

		var descriptorLabelCell = columns[4].ArrayValue[row];
		DescriptorLabel = !descriptorLabelCell.IsEmpty ? descriptorLabelCell.CellValue.StringValue : String.Empty;

		var fDescriptorLabelCell = columns[5].ArrayValue[row];
		FDescriptorLabel = !fDescriptorLabelCell.IsEmpty ? fDescriptorLabelCell.CellValue.StringValue : String.Empty;

		ConnectedSource = destinationIdToSourceNameRelations.TryGetValue(Id, out string sourceName)? sourceName : string.Empty;
	}

	public string Name { get; private set; }

	public string Id { get; private set; }

	public string ConnectedSource { get; private set; }

	public string Description { get; private set; }

	public string[] Tags { get; private set; } = new string[0];

	public string DescriptorLabel { get; private set; }

	public string FDescriptorLabel { get; private set; }

	public bool IsValid()
	{
		if (String.IsNullOrEmpty(Name))
		{
			return false;
		}

		if (String.IsNullOrEmpty(Id))
		{
			return false;
		}

		if (String.IsNullOrEmpty(DescriptorLabel))
		{
			return false;
		}

		return true;
	}

	public bool MatchesTagFilter(params string[] filter)
	{
		if (filter == null || !filter.Any())
		{
			return true;
		}

		return Tags.Intersect(filter).Any();
	}

	public GQIRow ToGqiRow()
	{
		return new GQIRow(
			new[]
			{
				new GQICell { Value = DescriptorLabel },
				new GQICell { Value = ConnectedSource.Trim() },
				new GQICell { Value = String.Join(",", Tags) },
				new GQICell { Value = Id },
				new GQICell { Value = !String.IsNullOrWhiteSpace(ConnectedSource) },
			});
	}
}