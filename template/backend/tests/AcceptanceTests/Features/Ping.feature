Feature: Ping
  Anyone can check that the API is up and which API it is.

  Scenario: The API answers a ping
    When I ping the API
    Then it answers "ok"
    And it names itself

  Scenario: A client watches the API's pings
    When I watch the API's pings
    Then the first event is a ping that answers "ok"
