Feature: Notes
  A signed-in user keeps short notes that only they can read.

  Scenario: A user saves a note and reads it back
    Given I am signed in as "alice"
    When I save the note "buy milk"
    Then the note is created
    And I can read it back as "buy milk"

  Scenario: A note cannot be empty
    Given I am signed in as "alice"
    When I save the note " "
    Then the note is refused as invalid

  Scenario: Nobody else can read my note
    Given I am signed in as "alice"
    And I saved the note "buy milk"
    When I am signed in as "bob"
    Then I cannot read alice's note

  Scenario: A visitor who is not signed in cannot save a note
    When I save the note "buy milk"
    Then I am told to sign in
