# Marketing Coding Test

Marketing coding test.

## Implemented Features

### Basic Features

- **Display Voting Average**: ✔️

- **Voting Average Filter**: ✔️

- **Release Date Display**: ✔️

- **Date Range Filter**: ✔️

- **Improved Styling**: ✔️
  - **Enhanced Styling**: Leveraged Bootstrap to create a clean, modern interface with impoved layout and visual hierarchy
  - **Responsive Design**: Ensured application works seamlessly across different screen sizes

### Advanced Features

- **Autocomplete Feature**: ✔️
  - **Notes**: Based on existing search index - so that autocomplete suggests real titles, not random words.
  - **Technical Deep Dive**: This feature required extensive research into Lucene's archtitecture, including how search engine process and match text queries.

### Also

- **Seamless First Run**: Application now automatically builds the search (and autosuggest) index on startup; no need to manually rebuild when opening for the first time.

### Prerequisites

- .NET 6.0
- Visual Studio 2022 (ensure this is the default editor for .sln files)

## To run the app

1. In Git Bash, clone the project and open it in Visual Studio:
   ```bash
   git clone https://github.com/savocc/marketing-coding-assignment.git
   cd marketing-coding-assignment
   cd MarketingCodingAssignment
   start MarketingCodingAssignment.sln
   ```
2. Start the project (F5 or Ctrl+F5)

## Reflection

This assignment gave me valuable hands-on experience with search engine technology that I've never worked with before. The autocomplete feature was particularly challenging, required me to deep dive into Lucene's documentation and learn the fundamental concepts. I chose to implement the autocomplete feature not only because it was challenging, but also because it was a great candidate to show synergy between frontend and backend functionality, UI and UX.
