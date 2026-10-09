#pragma once

#include "custom-types/shared/macros.hpp"
#include "HMUI/ViewController.hpp"
#include "TMPro/TextMeshProUGUI.hpp"
#include "UnityEngine/GameObject.hpp"
#include "UnityEngine/UI/Button.hpp"
#include "UnityEngine/UI/VerticalLayoutGroup.hpp"

DECLARE_CLASS_CODEGEN(TA, TournamentViewController, HMUI::ViewController) {
    DECLARE_OVERRIDE_METHOD_MATCH(void, DidActivate, &HMUI::ViewController::DidActivate, bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling);
    DECLARE_OVERRIDE_METHOD_MATCH(void, DidDeactivate, &HMUI::ViewController::DidDeactivate, bool removedFromHierarchy, bool screenSystemDisabling);
    DECLARE_INSTANCE_METHOD(void, Refresh);

private:
    DECLARE_INSTANCE_FIELD(UnityEngine::UI::VerticalLayoutGroup*, rootLayout);
    DECLARE_INSTANCE_FIELD(UnityEngine::UI::VerticalLayoutGroup*, listLayout);
    DECLARE_INSTANCE_FIELD(UnityEngine::GameObject*, detailScroll);
    DECLARE_INSTANCE_FIELD(UnityEngine::UI::Button*, reconnectButton);
    DECLARE_INSTANCE_FIELD(TMPro::TextMeshProUGUI*, titleText);
    DECLARE_INSTANCE_FIELD(TMPro::TextMeshProUGUI*, statusText);
};
