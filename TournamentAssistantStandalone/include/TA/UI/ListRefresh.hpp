#pragma once

#include "UnityEngine/GameObject.hpp"
#include "beatsaber-hook/shared/utils/typedefs-wrappers.hpp"
#include "bsml/shared/BSML/Components/CustomListTableData.hpp"
#include "bsml/shared/BSML/MainThreadScheduler.hpp"

namespace TA::UI {
    inline void scheduleListRefresh(BSML::CustomListTableData* list) {
        if (!list || !list->tableView) return;

        // Keep both the table and its data source rooted until the callback runs.
        // A refresh or scene change may destroy them before then.
        SafePtrUnity<BSML::CustomListTableData> source(list);
        SafePtrUnity<HMUI::TableView> table(list->tableView);
        BSML::MainThreadScheduler::Schedule([source, table] {
            if (!source || !table) return;
            if (!source->get_gameObject()->get_activeInHierarchy()) return;
            table->ReloadData();
            // ReloadData can invoke callbacks that retire this list.
            if (!source || !table || !source->get_gameObject()->get_activeInHierarchy()) return;
            table->ClearSelection();
        });
    }
}
