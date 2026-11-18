#include <QPushButton>

#include <nav2_rviz_plugins/nav2_panel.hpp>
#include <pluginlib/class_list_macros.hpp>
#include <rviz_common/display_context.hpp>
#include <rviz_common/display_group.hpp>
#include <rviz_default_plugins/displays/marker_array/marker_array_display.hpp>

namespace scout_viz
{
class NavigationPanel : public nav2_rviz_plugins::Nav2Panel
{
public:
  NavigationPanel()
  {
    // Read the Humble panel's current button state before its Qt state-machine
    // transition, but only clear when the press actually becomes a click.
    for (auto * button : findChildren<QPushButton *>()) {
      connect(button, &QPushButton::pressed, this, [this, button]() {
        const QString text = button->text();
        clear_on_click_ = text == "Cancel" || text == "Cancel Accumulation" ||
          text == "Waypoint / Nav Through Poses Mode" ? button : nullptr;
      });
      connect(button, &QPushButton::clicked, this, [this, button]() {
        if (clear_on_click_ != button) {return;}
        clear_on_click_ = nullptr;
        if (auto * context = getDisplayContext()) {
          clearWaypoints(context->getRootDisplayGroup());
          context->queueRender();
        }
      });
    }
  }

private:
  static void clearWaypoints(rviz_common::DisplayGroup * group)
  {
    if (!group) {return;}
    for (int index = 0; index < group->numDisplays(); ++index) {
      auto * display = group->getDisplayAt(index);
      if (auto * nested = group->getGroupAt(index)) {
        clearWaypoints(nested);
      } else if (dynamic_cast<rviz_default_plugins::displays::MarkerArrayDisplay *>(display) &&
        display->subProp("Topic")->getValue().toString() == "/waypoints")
      {
        display->reset();
      }
    }
  }

  QPushButton * clear_on_click_{nullptr};
};
}  // namespace scout_viz

PLUGINLIB_EXPORT_CLASS(scout_viz::NavigationPanel, rviz_common::Panel)
