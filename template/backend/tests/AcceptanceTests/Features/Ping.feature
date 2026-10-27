Feature: Ping
  Anyone can check that the API is up and which API it is.

  Scenario: The API answers a ping
    When I ping the API
    Then it answers "ok"
    And it names itself
