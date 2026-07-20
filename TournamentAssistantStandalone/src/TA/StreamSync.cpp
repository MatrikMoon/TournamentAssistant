#include "TA/StreamSync.hpp"

#include "TA/AntiPause.hpp"
#include "main.hpp"

#include "GlobalNamespace/PauseController.hpp"
#include "GlobalNamespace/LevelBar.hpp"
#include "GlobalNamespace/PauseMenuManager.hpp"
#include "TMPro/TextMeshProUGUI.hpp"
#include "UnityEngine/Camera.hpp"
#include "UnityEngine/Color.hpp"
#include "UnityEngine/FilterMode.hpp"
#include "UnityEngine/GameObject.hpp"
#include "UnityEngine/ImageConversion.hpp"
#include "UnityEngine/Material.hpp"
#include "UnityEngine/MeshRenderer.hpp"
#include "UnityEngine/Object.hpp"
#include "UnityEngine/PrimitiveType.hpp"
#include "UnityEngine/Quaternion.hpp"
#include "UnityEngine/Renderer.hpp"
#include "UnityEngine/Rendering/ShadowCastingMode.hpp"
#include "UnityEngine/Resources.hpp"
#include "UnityEngine/Shader.hpp"
#include "UnityEngine/Texture.hpp"
#include "UnityEngine/Texture2D.hpp"
#include "UnityEngine/TextureWrapMode.hpp"
#include "UnityEngine/Transform.hpp"
#include "UnityEngine/Vector3.hpp"
#include "UnityEngine/UI/Button.hpp"

#include "bsml/shared/BSML/MainThreadScheduler.hpp"

#include <algorithm>
#include <cctype>
#include <cstdlib>
#include <mutex>
#include <optional>

namespace TA::StreamSync {
    namespace {
        UnityEngine::GameObject* overlayObject = nullptr;
        UnityEngine::MeshRenderer* overlayRenderer = nullptr;
        UnityEngine::Material* overlayMaterial = nullptr;
        UnityEngine::Texture2D* overlayTexture = nullptr;
        UnityEngine::Texture2D* solidColorTexture = nullptr;
        std::vector<uint8_t> imageBytes;
        std::mutex imageMutex;
        StringW oldSongNameText = nullptr;
        StringW oldAuthorNameText = nullptr;
        bool oldDifficultyActive = true;
        bool pauseMenuTextCaptured = false;
        bool active = false;

        std::optional<uint8_t> parseByte(std::string const& color, size_t offset) {
            if (offset + 2 > color.size()) return std::nullopt;
            auto hex = color.substr(offset, 2);
            char* end = nullptr;
            auto value = std::strtol(hex.c_str(), &end, 16);
            if (!end || *end != '\0' || value < 0 || value > 255) return std::nullopt;
            return uint8_t(value);
        }

        UnityEngine::Color parseColor(std::string color) {
            if (!color.empty() && color[0] == '#') color.erase(color.begin());
            if (color.size() != 6) {
                PaperLogger.warn("Invalid streamsync color '{}', falling back to black", color);
                return UnityEngine::Color::get_black();
            }
            auto r = parseByte(color, 0).value_or(0);
            auto g = parseByte(color, 2).value_or(0);
            auto b = parseByte(color, 4).value_or(0);
            return UnityEngine::Color(float(r) / 255.0f, float(g) / 255.0f, float(b) / 255.0f, 1.0f);
        }

        UnityEngine::Shader* findStreamSyncShader() {
            for (auto const* shaderName : {"Unlit/Texture", "Sprites/Default", "Standard"}) {
                auto shader = UnityEngine::Shader::Find(StringW(shaderName));
                if (shader) {
                    PaperLogger.info("Streamsync overlay using shader '{}'", shaderName);
                    return shader.ptr();
                }
            }
            PaperLogger.warn("Streamsync overlay could not find a usable shader");
            return nullptr;
        }

        void applyTexture(UnityEngine::Texture* texture, UnityEngine::Color color) {
            if (!overlayMaterial) return;
            overlayMaterial->set_mainTexture(texture);
            overlayMaterial->SetTexture(StringW("_MainTex"), texture);
            overlayMaterial->set_color(color);
            overlayMaterial->SetColor(StringW("_Color"), color);
            overlayMaterial->SetColor(StringW("_BaseColor"), color);
        }

        UnityEngine::Texture2D* makeSolidColorTexture(UnityEngine::Color color) {
            if (!solidColorTexture) {
                solidColorTexture = UnityEngine::Texture2D::New_ctor(1, 1);
                solidColorTexture->set_wrapMode(UnityEngine::TextureWrapMode::Clamp);
                solidColorTexture->set_filterMode(UnityEngine::FilterMode::Point);
            }
            solidColorTexture->SetPixel(0, 0, color);
            solidColorTexture->Apply(false, false);
            return solidColorTexture;
        }

        UnityEngine::Camera* streamCamera() {
            auto mainCamera = UnityEngine::Camera::get_main();
            if (mainCamera) return mainCamera.ptr();

            auto cameras = UnityEngine::Camera::get_allCameras();
            for (auto camera : cameras) {
                if (camera) return camera.ptr();
            }
            return nullptr;
        }

        void placeOverlayInView() {
            if (!overlayObject) return;

            auto* camera = streamCamera();
            if (!camera) {
                PaperLogger.warn("Streamsync POV overlay could not find a camera");
                return;
            }

            auto transform = overlayObject->get_transform();
            auto cameraTransform = camera->get_transform();
            if (!transform || !cameraTransform) return;

            transform->SetParent(cameraTransform.ptr(), false);
            transform->set_localPosition(UnityEngine::Vector3(0.0f, 0.0f, 1.0f));
            transform->set_localRotation(UnityEngine::Quaternion::get_identity());
            transform->set_localScale(UnityEngine::Vector3(3.0f, 1.7f, 1.0f));
        }

        void ensureOverlay() {
            if (overlayObject && overlayRenderer && overlayMaterial) return;

            PaperLogger.info("Creating streamsync POV mesh overlay");
            overlayObject = UnityEngine::GameObject::CreatePrimitive(UnityEngine::PrimitiveType::Quad).ptr();
            overlayRenderer = overlayObject->GetComponent<UnityEngine::MeshRenderer*>();
            if (overlayRenderer) {
                overlayRenderer->set_shadowCastingMode(UnityEngine::Rendering::ShadowCastingMode::Off);
                overlayRenderer->set_receiveShadows(false);
                overlayRenderer->set_sortingOrder(32767);
                if (auto* shader = findStreamSyncShader()) {
                    overlayMaterial = UnityEngine::Material::New_ctor(shader);
                    overlayMaterial->set_renderQueue(5000);
                    overlayRenderer->set_material(overlayMaterial);
                }
            }

            placeOverlayInView();
        }

        void restorePauseMenuText() {
            if (!pauseMenuTextCaptured) return;
            auto managers = UnityEngine::Resources::FindObjectsOfTypeAll<GlobalNamespace::PauseMenuManager*>();
            for (auto* manager : managers) {
                if (!manager) continue;
                auto levelBar = manager->__cordl_internal_get__levelBar();
                if (!levelBar) continue;

                auto songNameText = levelBar->__cordl_internal_get__songNameText();
                if (songNameText) songNameText->set_text(oldSongNameText);
                auto authorNameText = levelBar->__cordl_internal_get__authorNameText();
                if (authorNameText) authorNameText->set_text(oldAuthorNameText);
                auto difficultyText = levelBar->__cordl_internal_get__difficultyText();
                if (difficultyText && difficultyText->get_gameObject()) difficultyText->get_gameObject()->SetActive(oldDifficultyActive);
                levelBar->set_hide(false);
            }
            oldSongNameText = nullptr;
            oldAuthorNameText = nullptr;
            oldDifficultyActive = true;
            pauseMenuTextCaptured = false;
        }

        void clearOnMain(bool endStreamSync) {
            PaperLogger.info("Streamsync clear");
            if (endStreamSync) {
                active = false;
                restorePauseMenuText();
            }
            if (overlayTexture) {
                UnityEngine::Object::Destroy(overlayTexture);
                overlayTexture = nullptr;
            }
            if (solidColorTexture) {
                UnityEngine::Object::Destroy(solidColorTexture);
                solidColorTexture = nullptr;
            }
            if (overlayMaterial) {
                UnityEngine::Object::Destroy(overlayMaterial);
                overlayMaterial = nullptr;
            }
            if (overlayObject) {
                UnityEngine::Object::Destroy(overlayObject);
                overlayObject = nullptr;
                overlayRenderer = nullptr;
            }
        }

        void showColorOnMain(std::string color) {
            PaperLogger.info("Streamsync showColor '{}'", color);
            ensureOverlay();
            placeOverlayInView();
            if (!overlayMaterial) return;
            auto parsedColor = parseColor(std::move(color));
            auto* texture = makeSolidColorTexture(parsedColor);
            applyTexture(texture, UnityEngine::Color::get_white());
        }

        void showImageOnMain(bool show) {
            std::vector<uint8_t> bytesCopy;
            {
                std::scoped_lock lock(imageMutex);
                bytesCopy = imageBytes;
            }

            PaperLogger.info("Streamsync showImage show={} bytes={}", show, bytesCopy.size());
            ensureOverlay();
            placeOverlayInView();
            if (!overlayMaterial) return;
            if (!show) {
                if (overlayTexture) {
                    UnityEngine::Object::Destroy(overlayTexture);
                    overlayTexture = nullptr;
                }
                showColorOnMain("#000000");
                return;
            }
            if (bytesCopy.empty()) {
                PaperLogger.warn("Streamsync showImage requested before image bytes were preloaded");
                showColorOnMain("#000000");
                return;
            }
            if (overlayTexture) {
                UnityEngine::Object::Destroy(overlayTexture);
                overlayTexture = nullptr;
            }
            overlayTexture = UnityEngine::Texture2D::New_ctor(2, 2);
            auto bytes = ArrayW<uint8_t>(bytesCopy.size());
            std::copy(bytesCopy.begin(), bytesCopy.end(), bytes.begin());
            if (!UnityEngine::ImageConversion::LoadImage(overlayTexture, bytes)) {
                PaperLogger.warn("Streamsync image failed to decode");
                showColorOnMain("#000000");
                return;
            }
            overlayTexture->set_wrapMode(UnityEngine::TextureWrapMode::Clamp);
            overlayTexture->set_filterMode(UnityEngine::FilterMode::Bilinear);
            applyTexture(overlayTexture, UnityEngine::Color::get_white());
        }

        void applyPauseMenuText(GlobalNamespace::PauseMenuManager* manager) {
            if (!manager) return;
            auto levelBar = manager->__cordl_internal_get__levelBar();
            if (!levelBar) return;

            auto songNameText = levelBar->__cordl_internal_get__songNameText();
            auto authorNameText = levelBar->__cordl_internal_get__authorNameText();
            auto difficultyText = levelBar->__cordl_internal_get__difficultyText();
            if (!songNameText || !authorNameText) return;

            if (!pauseMenuTextCaptured) {
                oldSongNameText = songNameText->get_text();
                oldAuthorNameText = authorNameText->get_text();
                oldDifficultyActive = difficultyText && difficultyText->get_gameObject() ? difficultyText->get_gameObject()->get_activeSelf() : true;
                pauseMenuTextCaptured = true;
            }

            levelBar->set_hide(false);
            songNameText->set_text(StringW("Streamsync in progress..."));
            authorNameText->set_text(StringW("Setting up syncronised streams, hold tight"));
            if (difficultyText && difficultyText->get_gameObject()) difficultyText->get_gameObject()->SetActive(false);
        }

        void hidePauseMenuButtons() {
            auto managers = UnityEngine::Resources::FindObjectsOfTypeAll<GlobalNamespace::PauseMenuManager*>();
            PaperLogger.info("Streamsync hiding pause menu buttons managers={}", managers.size());
            for (auto* manager : managers) {
                if (!manager) continue;
                if (auto* initData = manager->__cordl_internal_get__initData()) {
                    initData->__cordl_internal_set_showRestartButton(false);
                }
                auto continueButton = manager->__cordl_internal_get__continueButton();
                if (continueButton && continueButton->get_gameObject()) continueButton->get_gameObject()->SetActive(false);
                auto restartButton = manager->__cordl_internal_get__restartButton();
                if (restartButton && restartButton->get_gameObject()) restartButton->get_gameObject()->SetActive(false);
                auto backButton = manager->__cordl_internal_get__backButton();
                if (backButton && backButton->get_gameObject()) backButton->get_gameObject()->SetActive(false);
                applyPauseMenuText(manager);
            }
        }

        void scheduleHidePauseMenuButtons(int attempt) {
            if (!active || attempt > 30) return;
            BSML::MainThreadScheduler::ScheduleAfterTime(0.1f, [attempt] {
                if (!active) return;
                hidePauseMenuButtons();
                scheduleHidePauseMenuButtons(attempt + 1);
            });
        }

        bool pauseGame() {
            auto controllers = UnityEngine::Resources::FindObjectsOfTypeAll<GlobalNamespace::PauseController*>();
            PaperLogger.info("Streamsync pause attempt controllers={}", controllers.size());
            for (auto* controller : controllers) {
                if (!controller) continue;
                TA::AntiPause::setAllowPause(true);
                controller->Pause();
                TA::AntiPause::setAllowPause(false);
                TA::AntiPause::setAllowContinueAfterPause(false);
                TA::AntiPause::setAllowRestart(false);
                hidePauseMenuButtons();
                scheduleHidePauseMenuButtons(0);
                return true;
            }
            return false;
        }

        bool continueGame() {
            auto controllers = UnityEngine::Resources::FindObjectsOfTypeAll<GlobalNamespace::PauseController*>();
            PaperLogger.info("Streamsync continue attempt controllers={}", controllers.size());
            for (auto* controller : controllers) {
                if (!controller) continue;
                auto pausedState = int32_t(controller->__cordl_internal_get__paused());
                if (pausedState == 2) {
                    PaperLogger.info("Streamsync continue skipped because game is already playing");
                    return true;
                }
                if (pausedState != 0 || !controller->get_canChangePauseState()) {
                    PaperLogger.info(
                        "Streamsync continue deferred pausedState={} canChange={}",
                        pausedState,
                        controller->get_canChangePauseState()
                    );
                    return false;
                }
                TA::AntiPause::setAllowPause(true);
                TA::AntiPause::setAllowContinueAfterPause(true);
                TA::AntiPause::setAllowRestart(true);
                controller->HandlePauseMenuManagerDidPressContinueButton();
                return true;
            }
            return false;
        }

        void scheduleContinueAttempt(int attempt) {
            BSML::MainThreadScheduler::ScheduleAfterTime(0.15f, [attempt] {
                if (continueGame()) {
                    TA::AntiPause::reset();
                    return;
                }
                if (attempt < 40) {
                    scheduleContinueAttempt(attempt + 1);
                } else {
                    PaperLogger.warn("Streamsync could not safely resume after repeated attempts; allowing manual continue");
                    TA::AntiPause::reset();
                }
            });
        }

        void schedulePauseAttempt(int attempt) {
            if (!active) return;
            BSML::MainThreadScheduler::ScheduleAfterTime(0.25f, [attempt] {
                if (!active) return;
                if (!pauseGame() && attempt < 40) {
                    schedulePauseAttempt(attempt + 1);
                } else if (attempt >= 40) {
                    PaperLogger.warn("Streamsync could not find a PauseController after repeated attempts");
                }
            });
        }
    }

    void begin() {
        PaperLogger.info("Streamsync begin");
        active = true;
        BSML::MainThreadScheduler::Schedule([] {
            ensureOverlay();
            showColorOnMain("#000000");
            schedulePauseAttempt(0);
            scheduleHidePauseMenuButtons(0);
        });
    }

    void finish() {
        PaperLogger.info("Streamsync finish");
        active = false;
        BSML::MainThreadScheduler::Schedule([] {
            clearOnMain(true);
            scheduleContinueAttempt(0);
        });
    }

    void clear() {
        BSML::MainThreadScheduler::Schedule([] {
            clearOnMain(true);
        });
    }

    void clearImmediate() {
        clearOnMain(true);
    }

    void showColor(std::string color) {
        BSML::MainThreadScheduler::Schedule([color = std::move(color)] {
            showColorOnMain(color);
        });
    }

    void setImage(std::vector<uint8_t> bytes) {
        PaperLogger.info("Streamsync setImage bytes={}", bytes.size());
        std::scoped_lock lock(imageMutex);
        imageBytes = std::move(bytes);
    }

    void showImage(bool show) {
        BSML::MainThreadScheduler::Schedule([show] {
            showImageOnMain(show);
        });
    }
}
