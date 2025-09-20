// Coral.Essentia.Cli.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <iostream>
#include <essentia_wrapper.h>
#include <filesystem>
namespace fs = std::filesystem;

std::vector<std::string> findM4AFiles(const std::string& directory_path, bool verbose = false) {
    std::vector<std::string> m4a_files;

    try {
        // Check if the directory exists
        if (!fs::exists(directory_path) || !fs::is_directory(directory_path)) {
            std::cerr << "Error: Directory does not exist or is not a directory: "
                << directory_path << std::endl;
            return m4a_files;
        }

        if (verbose) {
            std::cout << "Searching directory: " << fs::absolute(directory_path) << std::endl;
        }

        // Configure iterator options to skip problematic directories but continue
        auto options = fs::directory_options::skip_permission_denied;
        std::error_code ec;

        // Create iterator with error handling
        auto iterator = fs::recursive_directory_iterator(directory_path, options, ec);
        auto end = fs::recursive_directory_iterator{};

        if (ec) {
            std::cerr << "Error creating directory iterator: " << ec.message() << std::endl;
            return m4a_files;
        }

        // Recursively iterate through all directories
        while (iterator != end) {
            const auto& entry = *iterator;
            std::error_code entry_ec;

            if (verbose) {
                std::cout << "Checking: " << entry.path() << std::endl;
            }

            // Check if it's a regular file
            bool is_regular = entry.is_regular_file(entry_ec);
            if (entry_ec) {
                if (verbose) {
                    std::cout << "  Warning: Cannot check file type: " << entry_ec.message() << std::endl;
                }
                // Try to advance iterator and continue
                iterator.increment(entry_ec);
                if (entry_ec && verbose) {
                    std::cout << "  Warning: Cannot advance iterator: " << entry_ec.message() << std::endl;
                }
                continue;
            }

            if (is_regular) {
                std::string extension = entry.path().extension().string();

                // Convert extension to lowercase for case-insensitive comparison
                std::transform(extension.begin(), extension.end(), extension.begin(), ::tolower);

                if (verbose) {
                    std::cout << "  File extension: '" << extension << "'" << std::endl;
                }

                if (extension == ".m4a") {
                    m4a_files.push_back(entry.path().string());
                    if (verbose) {
                        std::cout << "  ✓ Found M4A file!" << std::endl;
                    }
                }
            }

            // Safely advance to next entry
            std::error_code advance_ec;
            iterator.increment(advance_ec);
            if (advance_ec) {
                if (verbose) {
                    std::cout << "Warning: Error advancing iterator: " << advance_ec.message() << std::endl;
                }
                break; // Exit if we can't advance
            }
        }
    }
    catch (const fs::filesystem_error& ex) {
        std::cerr << "Filesystem error: " << ex.what() << std::endl;
    }
    catch (const std::exception& ex) {
        std::cerr << "General error: " << ex.what() << std::endl;
    }

    return m4a_files;
}

int main()
{
    auto audioFileDir = "C:\\Music";
    auto files = findM4AFiles(audioFileDir, true);


    for (const auto& file : files) {
        auto ctxId = ew_create_context();
        auto modelPath = "C:\\Users\\bootie-\\Downloads\\discogs_track_embeddings-effnet-bs64-1.pb";
        ew_configure_tf_model(ctxId, modelPath);
        ew_run_inference(ctxId, file.c_str(), 16000, 4);
        ew_destroy_context(ctxId);
    }

}

// Run program: Ctrl + F5 or Debug > Start Without Debugging menu
// Debug program: F5 or Debug > Start Debugging menu

// Tips for Getting Started: 
//   1. Use the Solution Explorer window to add/manage files
//   2. Use the Team Explorer window to connect to source control
//   3. Use the Output window to see build output and other messages
//   4. Use the Error List window to view errors
//   5. Go to Project > Add New Item to create new code files, or Project > Add Existing Item to add existing code files to the project
//   6. In the future, to open this project again, go to File > Open > Project and select the .sln file
