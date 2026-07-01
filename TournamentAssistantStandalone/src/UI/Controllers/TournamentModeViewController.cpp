#include "TA/UI/TournamentModeViewController.hpp"

#include "main.hpp"

#include "TMPro/TextAlignmentOptions.hpp"
#include "UnityEngine/GameObject.hpp"
#include "UnityEngine/UI/Button.hpp"
#include "UnityEngine/UI/LayoutElement.hpp"

#include "bsml/shared/BSML-Lite.hpp"

using namespace BSML::Lite;

namespace {
    void setLayoutSize(UnityEngine::GameObject* object, float width, float height) {
        if (!object) return;
        auto* layout = object->GetComponent<UnityEngine::UI::LayoutElement*>();
        if (!layout) layout = object->AddComponent<UnityEngine::UI::LayoutElement*>();
        if (!layout) return;
        layout->set_preferredWidth(width);
        layout->set_preferredHeight(height);
        layout->set_minWidth(width);
        layout->set_minHeight(height);
    }

    void sizeButton(UnityEngine::UI::Button* button, float width, float height, float textSize) {
        if (!button) return;
        setLayoutSize(button->get_gameObject(), width, height);
        SetButtonTextSize(button, textSize);
        ToggleButtonWordWrapping(button, true);
    }

    TMPro::TextMeshProUGUI* addText(UnityEngine::Transform* parent, std::string const& value, float size) {
        auto* text = CreateText(parent, value);
        if (!text) return nullptr;
        text->set_fontSize(size);
        text->set_alignment(TMPro::TextAlignmentOptions::Center);
        return text;
    }

    std::string tournamentName(TA::Tournament const& tournament) {
        if (!tournament.settings.tournamentName.empty()) return tournament.settings.tournamentName;
        if (!tournament.guid.empty()) return tournament.guid;
        return "Tournament";
    }

    std::string tournamentDetails(TA::Tournament const& tournament) {
        if (!tournament.server.name.empty()) return tournament.server.name;
        if (!tournament.server.address.empty()) return tournament.server.address + ":" + std::to_string(tournament.server.port);
        return std::to_string(tournament.users.size()) + " players, " +
               std::to_string(tournament.matches.size()) + " matches, " +
               std::to_string(tournament.qualifiers.size()) + " qualifiers";
    }
}

void TA::UI::TournamentModeViewController::Render(
    UnityEngine::Transform* parent,
    TA::Tournament const& tournament,
    ImageRenderer imageRenderer,
    Action onTournament,
    Action onQualifiers,
    Action onBack
) {
    PaperLogger.info(
        "TournamentModeViewController rendering guid='{}' showTournament={} showQualifier={}",
        tournament.guid,
        tournament.settings.showTournamentButton,
        tournament.settings.showQualifierButton
    );
    if (!parent) {
        PaperLogger.error("TournamentModeViewController parent is null");
        return;
    }

    if (imageRenderer) imageRenderer(parent, tournament, 16.0f);
    addText(parent, tournamentName(tournament), 4.4f);
    addText(parent, tournamentDetails(tournament), 3.1f);
    addText(parent, "Users: " + std::to_string(tournament.users.size()) +
        "   Matches: " + std::to_string(tournament.matches.size()) +
        "   Qualifiers: " + std::to_string(tournament.qualifiers.size()), 3.0f);

    if (tournament.settings.showTournamentButton) {
        auto* button = CreateUIButton(parent, "Tournament", UnityEngine::Vector2({0, 0}), UnityEngine::Vector2({58.0f, 10.0f}), [onTournament] {
            PaperLogger.info("Tournament mode selected");
            if (onTournament) onTournament();
        });
        sizeButton(button, 58.0f, 10.0f, 4.2f);
    }

    if (tournament.settings.showQualifierButton) {
        auto* button = CreateUIButton(parent, "Qualifiers", UnityEngine::Vector2({0, 0}), UnityEngine::Vector2({42.0f, 7.0f}), [onQualifiers] {
            PaperLogger.info("Qualifier mode selected");
            if (onQualifiers) onQualifiers();
        });
        sizeButton(button, 42.0f, 7.0f, 3.2f);
    }

    if (!tournament.settings.showTournamentButton && !tournament.settings.showQualifierButton) {
        addText(parent, "This tournament has no player entry buttons enabled.", 3.2f);
    }

    auto* back = CreateUIButton(parent, "Back", [onBack] {
        PaperLogger.info("Back from tournament mode page");
        if (onBack) onBack();
    });
    sizeButton(back, 32.0f, 7.0f, 3.1f);
}
