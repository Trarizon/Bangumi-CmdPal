using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using Trarizon.Bangumi.Api.Models.Abstractions;

namespace Trarizon.Bangumi.CommandPalette.Utilities;
internal static class BangumiHelpers
{
    public static Details ToDetails(ISubject subject, params ReadOnlySpan<IDetailsElement> moreDetailElements)
    {
        var detailElems = new List<IDetailsElement>();
        if (!string.IsNullOrEmpty(subject.ChineseName))
            detailElems.Add(new DetailsElement { Key = "中文名", Data = new DetailsLink { Text = subject.ChineseName } });
        detailElems.Add(new DetailsElement { Key = "类型", Data = new DetailsTags { Tags = [subject.Type.ToTag()] } });

        var rtn = new Details
        {
            Title = subject.Name,
            Metadata = [.. detailElems, .. moreDetailElements],
        };
        string? image = subject switch
        {
            ISubjectImagesProvider imagesProvider => imagesProvider.Images.Common,
            ISubjectImageUrlProvider imageUrlProvider => imageUrlProvider.ImageUrl,
            _ => null,
        };
        if (image is not null) {
            rtn.HeroImage = new IconInfo(image);
        }
        return rtn;
    }
}
