namespace Library
{
    using System;
    using System.Collections.Generic;
    using Skyline.DataMiner.Analytics.GenericInterface;
    using Skyline.DataMiner.Net.Messages;

    public static class NevionVideoIPathExtensions
    {
        public static HashSet<string> GetTagsForProfile(GQIDMS dms, int dataminerId, int elementId, string profile)
        {
            var profileTags = new HashSet<string>();

            if (String.IsNullOrEmpty(profile))
            {
                return profileTags;
            }

            if (dataminerId == -1 || elementId == -1)
            {
                return profileTags;
            }

            var tableId = 2400;
            var getPartialTableMessage = new GetPartialTableMessage(dataminerId, elementId, tableId, new[] { "forceFullTable=true" });
            var parameterChangeEventMessage = (ParameterChangeEventMessage)dms.SendMessage(getPartialTableMessage);
            if (parameterChangeEventMessage.NewValue?.ArrayValue == null)
            {
                return profileTags;
            }

            var columns = parameterChangeEventMessage.NewValue.ArrayValue;
            if (columns.Length < 4)
            {
                return profileTags;
            }

            for (int i = 0; i < columns[1].ArrayValue.Length; i++)
            {
                var profileNameCell = columns[1].ArrayValue[i];
                if (profileNameCell.IsEmpty)
                {
                    continue;
                }

                if (profileNameCell.CellValue.StringValue == profile)
                {
                    var profileTagsCell = columns[3].ArrayValue[i];
                    if (!profileTagsCell.IsEmpty)
                    {
                        var valueTags = profileTagsCell.CellValue.StringValue.Split(',');
                        foreach (var valueTag in valueTags)
                        {
                            if (String.IsNullOrEmpty(valueTag))
                            {
                                continue;
                            }

                            profileTags.Add(valueTag.Trim());
                        }
                    }

                    break;
                }
            }

            return profileTags;
        }
    }
}