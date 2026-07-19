#include "TA/UI/TournamentListViewController.hpp"

#include "main.hpp"

#include "UnityEngine/RectTransform.hpp"
#include "TMPro/TextAlignmentOptions.hpp"
#include "UnityEngine/GameObject.hpp"
#include "UnityEngine/UI/LayoutElement.hpp"

#include "bsml/shared/BSML/Components/CustomListTableData.hpp"
#include "bsml/shared/BSML/MainThreadScheduler.hpp"
#include "bsml/shared/BSML-Lite.hpp"
#include "bsml/shared/BSML-Lite/Creation/Lists.hpp"

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

void TA::UI::TournamentListViewController::Render(
    UnityEngine::Transform* parent,
    std::vector<TA::Tournament> const& tournaments,
    SpriteProvider spriteProvider,
    SelectCallback onSelected
) {
    PaperLogger.info("TournamentListViewController rendering {} tournaments", tournaments.size());
    if (!parent) {
        PaperLogger.error("TournamentListViewController parent is null");
        return;
    }

    if (tournaments.empty()) {
        addText(parent, "No tournaments available", 4.0f);
        return;
    }

    std::vector<std::string> ids;
    ids.reserve(tournaments.size());
    for (auto const& tournament : tournaments) {
        ids.emplace_back(tournament.guid);
    }

    auto* list = CreateScrollableList(parent, {0.0f, 0.0f}, {78.0f, 60.0f}, [ids, onSelected](int row) {
        PaperLogger.info("Tournament list selected row={} ids={}", row, ids.size());
        if (row < 0 || static_cast<size_t>(row) >= ids.size()) {
            PaperLogger.warn("Tournament list selection out of range row={} size={}", row, ids.size());
            return;
        }
        if (onSelected) onSelected(ids[static_cast<size_t>(row)]);
    });
    if (!list) {
        PaperLogger.error("CreateScrollableList returned null");
        addText(parent, "Tournament list failed to render", 4.0f);
        return;
    }

    setLayoutSize(list->get_gameObject(), 78.0f, 60.0f);
    if (auto rect = list->get_transform().cast<UnityEngine::RectTransform>()) {
        rect->set_sizeDelta({78.0f, 60.0f});
    }
    list->cellSize = 10.0f;
    list->expandCell = true;
    list->listStyle = BSML::CustomListTableData::ListStyle::List;

    auto data = ListW<BSML::CustomCellInfo*>::New();
    data->EnsureCapacity(tournaments.size());

    for (auto const& tournament : tournaments) {
        auto* sprite = spriteProvider ? spriteProvider(tournament) : nullptr;
        PaperLogger.info(
            "Tournament native row guid='{}' name='{}' image='{}' sprite={} showTournament={} showQualifier={}",
            tournament.guid,
            tournamentName(tournament),
            tournament.settings.tournamentImage,
            static_cast<void*>(sprite),
            tournament.settings.showTournamentButton,
            tournament.settings.showQualifierButton
        );
        data->Add(BSML::CustomCellInfo::construct(
            StringW(tournamentName(tournament)),
            StringW(tournamentDetails(tournament)),
            sprite
        ));
    }

    list->data = data;
    if (list->tableView) {
        PaperLogger.info("Reloading native tournament list data rows={}", data->get_Count());
        auto* tableView = list->tableView;
        BSML::MainThreadScheduler::Schedule([tableView] {
            if (!tableView) return;
            tableView->ReloadData();
            tableView->ClearSelection();
        });
    } else {
        PaperLogger.warn("Tournament list tableView is null after CreateScrollableList");
    }
}
