using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Trarizon.Bangumi.CommandPalette.Pages;
internal sealed partial class HelpPage : ContentPage
{
    private readonly MarkdownContent _content;

    public HelpPage()
    {
        _content = new MarkdownContent("""
            没错这个页面渲染有问题，但是我懒得改

            # Trarizon.Bangumi.CommandPalette 帮助
            本扩展可用于搜索以及收藏 [Bangumi](https://bgm.tv) 的条目，以及标记条目章节的进度。

            ## 搜索语法
            
            以空格为分割，':'开头为可指定选项，其他为搜索关键词。选项可以出现在开头或结尾。
            
            出现第一个值后，后续第一个选项为止的部分为实际搜索关键字，再后续的值会被忽略。
            
            例：

            ```
            :opt :lead search keywords: example :mid data :trailing :>>>
            ```

            上述输入中：

            - 搜索关键词为：`search keywords: example`，其中的冒号只要不出现在空格后，就不会当作选项
            - 指定选项为：`opt`, `lead`, `mid`, `trailing`, `>>>`

            可用选项如下：

            检测文本|含义
            :--  |:--
            `me` |搜索用户在看/在玩的条目
            `>>` |可以为任意数量的条目，表示翻页

            ## 指令

            面板右下角可以看到详细指令详情。

            对于任意条目项，按回车可以打开条目的bangumi页面。
            
            对于收藏条目项，按Ctrl+回车可以标记下一章节为看过，若看完则标记条目为看过。在更多选项中也可以直接标记条目为看过。
            """);
    }

    public override IContent[] GetContent() => [_content];
}
