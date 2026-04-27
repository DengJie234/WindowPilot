using System.Windows;
using WindowPilot.Models;

namespace WindowPilot.Views.Dialogs;

public partial class RuleEditorDialog : Window
{
    public RuleEditorDialog()
    {
        InitializeComponent();
        ActionTypeBox.ItemsSource = Enum.GetValues<RuleActionType>();
        ActionTypeBox.SelectedItem = RuleActionType.SetTopMost;
    }

    public WindowRule? Rule { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var rule = new WindowRule
        {
            Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "新规则" : NameBox.Text.Trim(),
            ProcessName = ProcessNameBox.Text.Trim(),
            TitleContains = TitleContainsBox.Text.Trim(),
            ClassName = ClassNameBox.Text.Trim(),
            ProcessPath = ProcessPathBox.Text.Trim(),
            Action = new WindowRuleAction
            {
                Type = ActionTypeBox.SelectedItem is RuleActionType actionType ? actionType : RuleActionType.SetTopMost
            },
            Priority = int.TryParse(PriorityBox.Text, out var priority) ? Math.Max(1, priority) : 100,
            CooldownSeconds = int.TryParse(CooldownBox.Text, out var cooldown) ? Math.Max(1, cooldown) : 10,
            IsEnabled = true
        };

        if (!WindowRule.HasMatchCondition(rule))
        {
            ErrorText.Text = "请至少填写一个匹配条件，例如进程名或窗口标题关键词。";
            return;
        }

        Rule = rule;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Rule = null;
        DialogResult = false;
    }
}
