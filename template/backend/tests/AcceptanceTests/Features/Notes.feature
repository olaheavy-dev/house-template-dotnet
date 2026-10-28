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

  Scenario: A user pages through their notes
    Given I am signed in as "pager"
    And I saved the note "one"
    And I saved the note "two"
    And I saved the note "three"
    And I saved the note "four"
    And I saved the note "five"
    When I list my notes 2 at a time
    Then I get pages of 2, 2 and 1 notes
    And every one of my notes appears once

  Scenario: A damaged page cursor is refused
    Given I am signed in as "pager"
    When I list my notes starting from the cursor "not a cursor!"
    Then the request is refused as invalid

  Scenario: A page cannot be bigger than 100 notes
    Given I am signed in as "pager"
    When I list my notes 101 at a time
    Then the request is refused as invalid

  Scenario: A visitor who is not signed in cannot save a note
    When I save the note "buy milk"
    Then I am told to sign in
